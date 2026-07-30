using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Worker;

public sealed partial class SentEvidencePollFunction(
    PollSentEvidence pollSentEvidence,
    ILogger<SentEvidencePollFunction> logger)
{
    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("sent-evidence-poll");

    [Function(nameof(SentEvidencePollFunction))]
    public async Task RunAsync(
        [TimerTrigger("%SentEvidencePollSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var result = await pollSentEvidence.ExecuteAsync(
            maximumPages: 5,
            maximumItemsPerPage: 50,
            WorkerActor,
            cancellationToken);
        LogPollOutcome(
            logger,
            result.PagesRead,
            result.ItemsHandled,
            result.TriageResponsesRecorded,
            result.ReportEvidenceRetained,
            result.UnlinkedItems,
            result.QuarantinedItems);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Polled {PageCount} approved-Sent pages and handled {ItemCount} immutable items: {TriageResponseCount} exact Triage responses, {ReportEvidenceCount} retained report evidence items, {UnlinkedCount} items left unlinked, and {QuarantineCount} quarantined items. No outbound email was sent and no receipt or delivery was claimed.")]
    private static partial void LogPollOutcome(
        ILogger logger,
        int pageCount,
        int itemCount,
        int triageResponseCount,
        int reportEvidenceCount,
        int unlinkedCount,
        int quarantineCount);
}

public sealed partial class DueWorkSweepFunction(
    RunDueChasers runDueChasers,
    ILogger<DueWorkSweepFunction> logger)
{
    [Function(nameof(DueWorkSweepFunction))]
    public async Task RunAsync(
        [TimerTrigger("%DueWorkSweepSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var result = await runDueChasers.ExecuteAsync(
            maximumItems: 50,
            cancellationToken);
        LogSweepOutcome(
            logger,
            result.ExaminedCount,
            result.GeneratedCount,
            result.ReplayCount,
            result.SupersededCount);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Examined {ExaminedCount} due-work occurrences and persisted {GeneratedCount} copyable chaser drafts; {ReplayCount} were replays and {SupersededCount} were superseded. No outbound communication was attempted and no sending, receipt, or delivery was claimed.")]
    private static partial void LogSweepOutcome(
        ILogger logger,
        int examinedCount,
        int generatedCount,
        int replayCount,
        int supersededCount);
}
