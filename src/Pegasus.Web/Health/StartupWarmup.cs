using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Web.Health;

/// <summary>
/// Whether the new instance has finished warming. It is ready once the
/// warm-up ends, or after <see cref="ReadyAfter"/> whatever the warm-up is
/// doing, so a slow database never keeps an instance out of service.
/// </summary>
internal sealed class StartupWarmupState
{
    public static readonly TimeSpan ReadyAfter = TimeSpan.FromSeconds(45);

    private readonly long startedAt = Stopwatch.GetTimestamp();
    private int completed;

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
/// Runs once when Web starts: builds the EF model and runs the Work Centre's
/// and the Case page's hot read shapes, so the first staff request after a
/// deploy does not pay for them. It only reads. Every step is best effort: a
/// failure is logged and the next step still runs. Setting
/// <c>Startup:Warmup</c> to false skips it and reports ready at once.
/// </summary>
internal sealed partial class StartupWarmup(
    IServiceScopeFactory scopes,
    StartupWarmupState state,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<StartupWarmup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Startup:Warmup", true))
        {
            state.Complete();
            return;
        }

        await Task.Yield();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        bounded.CancelAfter(StartupWarmupState.ReadyAfter);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await WarmAsync(bounded.Token);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogWarmupFinished(logger, elapsedMs);
        }
        catch (OperationCanceledException) when (bounded.IsCancellationRequested)
        {
            // Stopping, or the warm-up outlived its bound; readiness no longer waits.
        }
        finally
        {
            state.Complete();
        }
    }

    private async Task WarmAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // A staff identity no account holds. The reads it drives are the ones a
        // signed-in page makes; nothing here writes.
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var now = clock.GetUtcNow();

        await StepAsync("model", async () =>
        {
            var factory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            _ = context.Model;
            await context.Database.CanConnectAsync(cancellationToken);
        }, cancellationToken);
        await StepAsync("work-centre", async () =>
        {
            await services.GetRequiredService<IGetOperationsSnapshot>().ExecuteAsync(
                new NeedsAttentionQuery(actor, NeedsAttentionScope.Office, 1, null, now), cancellationToken);
            await services.GetRequiredService<IAiJobQueries>().ListOpenAsync(cancellationToken);
            await services.GetRequiredService<IAiDraftQueries>().ListOpenAsync(cancellationToken);
        }, cancellationToken);

        Guid? caseId = null;
        await StepAsync("recent-cases", async () =>
        {
            var feed = await services.GetRequiredService<IListRecentCases>().ExecuteAsync(
                actor, 1, markSeen: false, cancellationToken, now);
            caseId = feed.Page.Items.Select(item => (Guid?)item.CaseId).FirstOrDefault();
        }, cancellationToken);
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
        }, cancellationToken);
    }

    private async Task StepAsync(string step, Func<Task> read, CancellationToken cancellationToken)
    {
        try
        {
            await read();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogWarmupStepFailed(logger, step, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Startup warm-up finished in {ElapsedMs} ms")]
    private static partial void LogWarmupFinished(ILogger logger, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Startup warm-up step {Step} failed")]
    private static partial void LogWarmupStepFailed(ILogger logger, string step, Exception exception);
}
