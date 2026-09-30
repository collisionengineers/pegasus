using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
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
        [TimerTrigger("%SentEvidencePollSchedule%", RunOnStartup = false, UseMonitor = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var result = await pollSentEvidence.ExecuteBatchAsync(
            maximumMailboxes: 25,
            maximumPages: 5,
            maximumItemsPerPage: 50,
            WorkerActor,
            cancellationToken);
        LogPollOutcome(
            logger,
            result.MailboxesAttempted,
            result.MailboxesFailed,
            result.PagesRead,
            result.ItemsHandled,
            result.ReportEvidenceRetained,
            result.FirstFailure);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Polled {MailboxCount} approved Sent mailboxes ({FailureCount} failed), read {PageCount} pages, handled {ItemCount} immutable items, and retained {ReportEvidenceCount} report evidence items. First failure: {FirstFailure}. No outbound email was sent and no receipt or delivery was claimed.")]
    private static partial void LogPollOutcome(
        ILogger logger,
        int mailboxCount,
        int failureCount,
        int pageCount,
        int itemCount,
        int reportEvidenceCount,
        string? firstFailure);
}
