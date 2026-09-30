using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
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
/// It also carries how long the warm-up waits between its keep-warm passes.
/// </summary>
internal sealed class StartupWarmupState
{
    public static readonly TimeSpan ReadyAfter = TimeSpan.FromSeconds(45);

    /// <summary>The wait between keep-warm passes when <c>Startup:WarmupInterval</c> is not set.</summary>
    public static readonly TimeSpan DefaultKeepWarmInterval = TimeSpan.FromMinutes(3);

    private readonly long startedAt = Stopwatch.GetTimestamp();
    private int completed;

    public StartupWarmupState(bool warms, TimeSpan? keepWarmInterval = null)
    {
        Warms = warms;
        KeepWarmInterval = keepWarmInterval ?? DefaultKeepWarmInterval;
        completed = warms ? 0 : 1;
    }

    public bool Warms { get; }

    /// <summary>
    /// The wait between keep-warm passes, from the end of one to the start of
    /// the next. Zero or less means the warm-up runs its first pass and ends.
    /// </summary>
    public TimeSpan KeepWarmInterval { get; }

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
/// Runs when Web starts: loads the data-protection key ring, waits for the
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
/// <para>
/// After that first pass, which readiness waits for, the service runs a
/// keep-warm pass every <see cref="StartupWarmupState.KeepWarmInterval"/>
/// (<c>Startup:WarmupInterval</c>, three minutes) until the host stops. A pass
/// makes the reads a signed-in page makes, so no staff request is the first to
/// touch a cold path after a quiet spell: the Work Centre's three sections
/// together, as the page reads them, the Inbox list, the newest Case's page
/// and the report renderer. It never writes: the recent-Cases read does not
/// mark the feed seen. Each pass runs under one <c>Pegasus.Warmup</c> activity,
/// so its dependency rows share an operation id and it makes no request row.
/// </para>
/// </summary>
internal sealed partial class StartupWarmup(
    IServiceScopeFactory scopes,
    StartupWarmupState state,
    TimeProvider clock,
    ILogger<StartupWarmup> logger) : BackgroundService
{
    /// <summary>The name of the activity a keep-warm pass runs under.</summary>
    internal const string PassActivityName = "Pegasus.Warmup";

    /// <summary>The longest one step of a keep-warm pass may take.</summary>
    private static readonly TimeSpan KeepWarmStepBound = TimeSpan.FromSeconds(15);

    // A staff identity no account holds. The reads it drives are the ones a
    // signed-in page makes; nothing here writes.
    private readonly ActionActor actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    // The steps that have failed in a keep-warm pass. The first failure of a step
    // is a warning; later ones are debug lines, so a broken step does not write
    // a warning every three minutes. Only the keep-warm loop touches it.
    private readonly HashSet<string> failedSteps = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!state.Warms)
        {
            return;
        }

        await Task.Yield();
        await FirstPassAsync(stoppingToken);
        await KeepWarmAsync(stoppingToken);
    }

    private async Task FirstPassAsync(CancellationToken stoppingToken)
    {
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

    /// <summary>
    /// Waits, then runs one keep-warm pass, until the host stops. An interval of
    /// zero ends the warm-up after its first pass.
    /// </summary>
    private async Task KeepWarmAsync(CancellationToken stoppingToken)
    {
        var interval = state.KeepWarmInterval;
        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(interval, clock, stoppingToken);
                await KeepWarmPassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    /// <summary>
    /// One pass of the reads a signed-in page makes, each step bounded and best
    /// effort. Nothing it meets leaves it: a failure here would stop the host.
    /// </summary>
    private async Task KeepWarmPassAsync(CancellationToken stoppingToken)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            // One operation id for the pass's SQL and other dependency rows, and
            // no request row: an activity nobody listens to for a request.
            using var activity = new Activity(PassActivityName).Start();
            await using var scope = scopes.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var now = clock.GetUtcNow();

            Guid? caseId = null;
            await KeepWarmStepAsync("work-centre", async cancellationToken =>
            {
                // The page reads its three sections side by side, each on its own
                // database context and connection, so the pass touches the three
                // together. The drafts read follows the jobs read on the page.
                var attention = services.GetRequiredService<IGetOperationsSnapshot>().ExecuteAsync(
                    new NeedsAttentionQuery(actor, NeedsAttentionScope.Office, 1, null, now), cancellationToken);
                var newCases = services.GetRequiredService<IListRecentCases>().ExecuteAsync(
                    actor, 1, markSeen: false, cancellationToken, now);
                var aiJobs = services.GetRequiredService<IAiJobQueries>().ListOpenAsync(cancellationToken);
                await Task.WhenAll(attention, newCases, aiJobs);
                caseId = (await newCases).Page.Items.Select(item => (Guid?)item.CaseId).FirstOrDefault();
                await services.GetRequiredService<IAiDraftQueries>().ListOpenAsync(cancellationToken);
            }, stoppingToken);
            await KeepWarmStepAsync(
                "inbox", cancellationToken => ReadInboxAsync(services, cancellationToken), stoppingToken);
            if (caseId is { } id)
            {
                await KeepWarmStepAsync(
                    "case-page", cancellationToken => ReadCasePageAsync(services, id, cancellationToken), stoppingToken);
            }

            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogKeepWarmPassFinished(logger, elapsedMs);
        }
        catch (Exception exception)
        {
            if (!stoppingToken.IsCancellationRequested)
            {
                LogKeepWarmFailure("pass", exception, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }

    /// <summary>
    /// One best-effort step of a keep-warm pass, bounded at 15 s. A failure is
    /// logged and the next step runs; only the host stopping ends it quietly.
    /// </summary>
    private async Task KeepWarmStepAsync(
        string step,
        Func<CancellationToken, Task> read,
        CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            bound.CancelAfter(KeepWarmStepBound);
            await read(bound.Token);
        }
        catch (Exception exception)
        {
            // A cancelled SQL command surfaces as a database error, not as a
            // cancellation, so the bound is not asked about the exception type.
            if (!stoppingToken.IsCancellationRequested)
            {
                LogKeepWarmFailure(step, exception, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }

    private void LogKeepWarmFailure(string step, Exception exception, double elapsedMs)
    {
        var level = failedSteps.Add(step) ? LogLevel.Warning : LogLevel.Debug;
        LogKeepWarmStepFailed(logger, level, step, elapsedMs, exception);
    }

    /// <summary>
    /// The Inbox list as a fresh visit reads it: the mailboxes, one page of the
    /// default scope (every mailbox, the Inbox folder, newest first) and the
    /// freshness of the polling behind it.
    /// </summary>
    private async Task ReadInboxAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var mail = services.GetRequiredService<ListRetainedMail>();
        await mail.ListMailboxesAsync(actor, cancellationToken);
        await mail.ExecuteAsync(
            actor,
            new MailWorkspaceScope(null, MailFolderScope.Inbox),
            1,
            Pages.Mail.IndexModel.PageSize,
            cancellationToken);
        await services.GetRequiredService<GetRetainedMailFreshness>().ExecuteAsync(actor, cancellationToken);
    }

    /// <summary>The Case page's frame, access and workspace reads; a Triage Case has none.</summary>
    private async Task ReadCasePageAsync(IServiceProvider services, Guid id, CancellationToken cancellationToken)
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
    }

    private async Task WarmAsync(CancellationToken cancellationToken, CancellationToken stoppingToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
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
        await WarmDatabaseAsync(services, now, cancellationToken, stoppingToken);
        await Task.WhenAll(keyRing, certificates);
    }

    private async Task WarmDatabaseAsync(
        IServiceProvider services,
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

        await StepAsync(
            "case-page", () => ReadCasePageAsync(services, id, cancellationToken), stoppingToken, cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Warm-up pass finished in {ElapsedMs} ms")]
    private static partial void LogKeepWarmPassFinished(ILogger logger, double elapsedMs);

    [LoggerMessage(Message = "Warm-up step {Step} failed after {ElapsedMs} ms")]
    private static partial void LogKeepWarmStepFailed(
        ILogger logger, LogLevel level, string step, double elapsedMs, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup warm-up ended early")]
    private static partial void LogWarmupFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup warm-up step {Step} failed")]
    private static partial void LogWarmupStepFailed(ILogger logger, string step, Exception exception);
}
