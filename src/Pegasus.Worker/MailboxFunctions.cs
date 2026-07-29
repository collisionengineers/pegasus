using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Pegasus.Worker;

public sealed partial class ApprovedInboxPollingFunction(
    PollApprovedInbox pollApprovedInbox,
    ILogger<ApprovedInboxPollingFunction> logger)
{
    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("approved-inbox-poller");

    [Function(nameof(ApprovedInboxPollingFunction))]
    public async Task RunAsync(
        [TimerTrigger("%ApprovedInboxPollSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var received = await pollApprovedInbox.ExecuteAsync(
            50,
            WorkerActor,
            cancellationToken);
        LogApprovedInboxPoll(logger, received);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Received {ApprovedInboxMessageCount} immutable approved-inbox messages into durable intake.")]
    private static partial void LogApprovedInboxPoll(
        ILogger logger,
        int approvedInboxMessageCount);
}
