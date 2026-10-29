using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;

namespace Pegasus.Web.Background;

/// <summary>
/// Requests a Case's Glass's valuation after the command that brought it to
/// With Engineer has answered (R465, 28 October 2026). Glass's is reached over
/// the valuation account this host holds, so the request runs on this host's
/// provider work queue (ADR-0058, ADR-0060). A full queue requests nothing:
/// the command is never held on Glass's, and Get valuation stays as it is.
/// </summary>
public sealed partial class AutomaticGuideValuationScheduler(
    ProviderWorkQueue queue,
    ILogger<AutomaticGuideValuationScheduler> logger) : IScheduleAutomaticGuideValuation
{
    public const string Kind = "automatic-guide-valuation";

    public Task ScheduleAsync(Guid caseId, CancellationToken cancellationToken)
    {
        using var reservation = queue.Reserve(KeyOf(caseId));
        var work = new ProviderWork(reservation.Key, Kind, (scope, token) => RunAsync(scope, caseId, token));
        if (reservation.Admit(work) == ProviderWorkAdmission.Full)
        {
            LogNotQueued(logger, caseId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The work's own queue key. The Case's id is the key its valuation report
    /// is filed under, and that filing is scheduled from inside this work, so
    /// a key of its own keeps the two from waiting on each other.
    /// </summary>
    internal static Guid KeyOf(Guid caseId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{Kind}:{caseId:D}")).AsSpan(0, 16));

    private static async Task RunAsync(IServiceProvider scope, Guid caseId, CancellationToken cancellationToken)
    {
        var logger = scope.GetRequiredService<ILogger<AutomaticGuideValuationScheduler>>();
        try
        {
            var outcome = await scope.GetRequiredService<RunAutomaticGuideValuation>()
                .ExecuteAsync(caseId, cancellationToken);
            LogOutcome(logger, caseId, outcome);
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested)
        {
            LogFailed(logger, caseId, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "The automatic valuation for case {CaseId} ended {Outcome}")]
    private static partial void LogOutcome(ILogger logger, Guid caseId, AutomaticGuideValuationOutcome outcome);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The automatic valuation for case {CaseId} was not queued: the provider work queue is full")]
    private static partial void LogNotQueued(ILogger logger, Guid caseId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The automatic valuation for case {CaseId} failed")]
    private static partial void LogFailed(ILogger logger, Guid caseId, Exception exception);
}
