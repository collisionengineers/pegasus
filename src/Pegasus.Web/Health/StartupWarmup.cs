using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Microsoft.AspNetCore.DataProtection;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Health;

/// <summary>
/// Whether the new instance has finished warming. It is ready once the
/// warm-up ends, or after <see cref="ReadyAfter"/> whatever the warm-up is
/// doing, so a slow database never keeps an instance out of service. A host
/// that does not warm (<c>Startup:Warmup</c> false) is ready from the start.
/// </summary>
internal sealed class StartupWarmupState
{
    public static readonly TimeSpan ReadyAfter = TimeSpan.FromSeconds(45);

    private readonly long startedAt = Stopwatch.GetTimestamp();
    private int completed;

    public StartupWarmupState(bool warms)
    {
        Warms = warms;
        completed = warms ? 0 : 1;
    }

    public bool Warms { get; }

    public bool IsReady =>
        Volatile.Read(ref completed) == 1 || Stopwatch.GetElapsedTime(startedAt) >= ReadyAfter;

    public void Complete() => Volatile.Write(ref completed, 1);
}

/// <summary>The ready-tagged check that holds readiness until the warm-up ends.</summary>
internal sealed class StartupWarmupHealthCheck(StartupWarmupState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(state.IsReady
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("The instance is still warming."));
}

/// <summary>
/// Runs once when Web starts: loads the data-protection key ring, waits for the
/// Automation OAuth certificates, builds the EF model and runs the Work
/// Centre's, the Case page's and the Graph mail webhook's hot read shapes, so
/// the first request after a deploy does not pay for them. It is a hosted
/// service, so it begins with the host's start, a few seconds before
/// <c>ApplicationStarted</c> prints the "listening" mark (4 to 8 s on the starts
/// of 29 September 2026), and its first steps overlap the port binding. It
/// yields at once, so it never holds the port. The key ring and the
/// certificates are remote reads behind a managed-identity token, which is why
/// they are here and not before the port binds. Its database steps only read.
/// Every step is best effort: a failure is logged and the next step still runs,
/// and nothing it meets stops the host. Setting <c>Startup:Warmup</c> to false
/// skips it.
/// </summary>
internal sealed partial class StartupWarmup(
    IServiceScopeFactory scopes,
    StartupWarmupState state,
    TimeProvider clock,
    ILogger<StartupWarmup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!state.Warms)
        {
            return;
        }

        await Task.Yield();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        bounded.CancelAfter(StartupWarmupState.ReadyAfter);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await WarmAsync(bounded.Token, stoppingToken);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogWarmupFinished(logger, elapsedMs);
        }
        catch (Exception exception)
        {
            // Nothing leaves the warm-up: a failure here would stop the host.
            // A cancelled SQL command surfaces as a database error, not as a
            // cancellation, so the bound is not asked about the exception type.
            if (!stoppingToken.IsCancellationRequested)
            {
                LogWarmupFailed(logger, exception);
            }
        }
        finally
        {
            state.Complete();
        }
    }

    private async Task WarmAsync(CancellationToken cancellationToken, CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // A staff identity no account holds. The reads it drives are the ones a
        // signed-in page makes; nothing here writes.
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var now = clock.GetUtcNow();

        // Neither needs the database, so they run beside the reads below.
        var keyRing = StepAsync(
            "key-ring",
            () => Task.Run(() => services.GetRequiredService<IDataProtectionProvider>().LoadKeyRing())
                .WaitAsync(cancellationToken),
            stoppingToken,
            cancellationToken);
        var certificates = StepAsync(
            "oauth-certificates",
            () => services.GetService<OAuthCertificateStore>() is { } store
                ? store.Loaded.WaitAsync(cancellationToken)
                : Task.CompletedTask,
            stoppingToken,
            cancellationToken);
        await WarmDatabaseAsync(services, actor, now, cancellationToken, stoppingToken);
        await Task.WhenAll(keyRing, certificates);
    }

    private async Task WarmDatabaseAsync(
        IServiceProvider services,
        ActionActor actor,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        CancellationToken stoppingToken)
    {
        // The step's line carries two timings: the model build, which is local
        // CPU (it includes Entity Framework's own service provider, built on the
        // first use of the context), and the first connection, which holds the
        // managed-identity token and the SQL login. The next start says which
        // of the two the step's 17 to 34 s was.
        var modelBuild = TimeSpan.Zero;
        var firstConnection = TimeSpan.Zero;
        await StepAsync("model", async () =>
        {
            var factory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            var buildStarted = Stopwatch.GetTimestamp();
            _ = context.Model;
            modelBuild = Stopwatch.GetElapsedTime(buildStarted);
            var connectStarted = Stopwatch.GetTimestamp();
            await context.Database.CanConnectAsync(cancellationToken);
            firstConnection = Stopwatch.GetElapsedTime(connectStarted);
        }, stoppingToken, cancellationToken,
        detail: () => string.Create(
            CultureInfo.InvariantCulture,
            $"model build {modelBuild.TotalMilliseconds:F0} ms, first connection {firstConnection.TotalMilliseconds:F0} ms"));
        // Graph hangs up on a webhook that answers slowly and resends only
        // minutes later, so the first mail must not pay for this query.
        await StepAsync("mail-webhook", () =>
            services.GetRequiredService<IApprovedMailboxSubscriptionStore>().GetActiveAsync(
                "startup-warm-up", now, cancellationToken),
            stoppingToken, cancellationToken);
        await StepAsync("work-centre", async () =>
        {
            await services.GetRequiredService<IGetOperationsSnapshot>().ExecuteAsync(
                new NeedsAttentionQuery(actor, NeedsAttentionScope.Office, 1, null, now), cancellationToken);
            await services.GetRequiredService<IAiJobQueries>().ListOpenAsync(cancellationToken);
            await services.GetRequiredService<IAiDraftQueries>().ListOpenAsync(cancellationToken);
        }, stoppingToken, cancellationToken);

        Guid? caseId = null;
        await StepAsync("recent-cases", async () =>
        {
            var feed = await services.GetRequiredService<IListRecentCases>().ExecuteAsync(
                actor, 1, markSeen: false, cancellationToken, now);
            caseId = feed.Page.Items.Select(item => (Guid?)item.CaseId).FirstOrDefault();
        }, stoppingToken, cancellationToken);
        if (caseId is not { } id)
        {
            return;
        }

        await StepAsync("case-page", async () =>
        {
            if (await services.GetRequiredService<IGetCaseKind>().ExecuteAsync(id, cancellationToken)
                is null or CaseType.Triage)
            {
                return;
            }

            await services.GetRequiredService<IGetCasePageFrame>().ExecuteAsync(
                new(id, actor, Work: CaseWorkSelector.Current), cancellationToken);
            await services.GetRequiredService<IGetAssessmentAccess>().ExecuteAsync(
                new(id, actor), cancellationToken);
            await services.GetRequiredService<IGetAssessmentWorkspace>().ExecuteAsync(
                new(id, actor, CaseWorkSelector.Current), cancellationToken);
        }, stoppingToken, cancellationToken);
    }

    /// <summary>
    /// One best-effort read. Any failure is logged and the next step runs; a
    /// step past the bound fails fast. Only the host stopping ends it quietly.
    /// </summary>
    private async Task StepAsync(
        string step,
        Func<Task> read,
        CancellationToken stoppingToken,
        CancellationToken cancellationToken,
        Func<string>? detail = null)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await read();
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogWarmupStepFinished(logger, step, elapsedMilliseconds, detail is null ? string.Empty : $" ({detail()})");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                LogWarmupStepBounded(logger, step, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
        catch (Exception exception)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                LogWarmupStepFailed(logger, step, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Startup warm-up step {Step} finished in {ElapsedMs} ms{Detail}")]
    private static partial void LogWarmupStepFinished(ILogger logger, string step, double elapsedMs, string detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup warm-up step {Step} was still running after {ElapsedMs} ms and was left")]
    private static partial void LogWarmupStepBounded(ILogger logger, string step, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Startup warm-up finished in {ElapsedMs} ms")]
    private static partial void LogWarmupFinished(ILogger logger, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup warm-up ended early")]
    private static partial void LogWarmupFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup warm-up step {Step} failed")]
    private static partial void LogWarmupStepFailed(ILogger logger, string step, Exception exception);
}
