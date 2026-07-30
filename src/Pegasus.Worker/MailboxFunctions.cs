using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

public sealed partial class InboxPollFunction(
    PollApprovedInbox pollApprovedInbox,
    ILogger<InboxPollFunction> logger)
{
    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("approved-inbox-poller");

    [Function(nameof(InboxPollFunction))]
    public async Task RunAsync(
        [TimerTrigger("%ApprovedInboxPollSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var handled = await pollApprovedInbox.ExecuteAsync(
            50,
            WorkerActor,
            cancellationToken);
        LogApprovedInboxPoll(logger, handled);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Handled {ApprovedInboxMessageCount} immutable approved-inbox messages through durable intake or poison recovery.")]
    private static partial void LogApprovedInboxPoll(
        ILogger logger,
        int approvedInboxMessageCount);
}
