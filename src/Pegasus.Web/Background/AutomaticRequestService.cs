using Pegasus.Core.Assessment;

namespace Pegasus.Web.Background;

/// <summary>
/// Runs the automatic requests Case creation recorded (R545, R546, R1286):
/// every <c>Valuation:AutomaticPassInterval</c> (one minute unless set;
/// <c>00:00:00</c> turns the pass off) it takes the due requests of every kind
/// and runs each once, in its own scope. The valuation providers and Experian
/// are reached over the accounts this host holds, so the pass runs here and not
/// in the Worker (ADR-0060). A failure ends only that request's pass, and the
/// next pass reads it again.
/// </summary>
public sealed partial class AutomaticRequestService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<AutomaticRequestService> logger) : BackgroundService
{
    private const string IntervalKey = "Valuation:AutomaticPassInterval";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var interval = configuration.GetValue(IntervalKey, AutomaticRequestPolicy.Wait);
            if (interval <= TimeSpan.Zero)
            {
                return;
            }

            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
        catch (Exception exception)
        {
            // A background service that throws stops the host; this pass must not.
            LogPassFailed(logger, exception);
        }
    }

    private async Task PassAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<AutomaticRequest> due;
        try
        {
            await using var listScope = scopes.CreateAsyncScope();
            due = await listScope.ServiceProvider.GetRequiredService<RunAutomaticRequest>()
                .ListDueAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            LogPassFailed(logger, exception);
            return;
        }

        foreach (var request in due)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var outcome = await scope.ServiceProvider.GetRequiredService<RunAutomaticRequest>()
                    .ExecuteAsync(request, stoppingToken);
                if (outcome != AutomaticRequestOutcome.Waiting)
                {
                    LogOutcome(logger, request.Kind, request.CaseId, outcome);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(logger, request.Kind, request.CaseId, exception);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The automatic {Kind} for case {CaseId} ended {Outcome}")]
    private static partial void LogOutcome(
        ILogger logger, AutomaticRequestKind kind, Guid caseId, AutomaticRequestOutcome outcome);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The automatic {Kind} for case {CaseId} failed")]
    private static partial void LogFailed(ILogger logger, AutomaticRequestKind kind, Guid caseId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The automatic request pass could not read its requests")]
    private static partial void LogPassFailed(ILogger logger, Exception exception);
}
