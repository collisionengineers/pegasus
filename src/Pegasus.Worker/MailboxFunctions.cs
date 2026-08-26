using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Pegasus.Infrastructure.Email;

namespace Pegasus.Worker;

public sealed partial class InboxRecoveryFunction(
    PollApprovedInbox pollApprovedInbox,
    IApprovedMailboxSubscriptionStore subscriptions,
    IEnumerable<GraphMailboxChangeSubscriptions> graphSubscriptionProviders,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<InboxRecoveryFunction> logger)
{
    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("approved-inbox-poller");

    [Function(nameof(InboxRecoveryFunction))]
    public async Task RunAsync(
        [TimerTrigger("%ApprovedInboxPollSchedule%", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var graphSubscriptions = graphSubscriptionProviders.SingleOrDefault();
        if (graphSubscriptions is not null)
        {
            var callbackUri = new Uri(
                configuration["Graph:ChangeNotificationUrl"]
                ?? throw new InvalidOperationException("Graph:ChangeNotificationUrl is required."),
                UriKind.Absolute);
            var clientState = configuration["Graph:ChangeNotificationClientState"]
                ?? throw new InvalidOperationException("Graph:ChangeNotificationClientState is required.");
            var candidates = await subscriptions.ListMaintenanceCandidatesAsync(
                now,
                cancellationToken);
            foreach (var candidate in candidates)
            {
                try
                {
                    var maintained = GraphMailboxChangeSubscriptions.RequiresWrite(
                        candidate,
                        now,
                        now.AddHours(48))
                        ? await graphSubscriptions.MaintainAsync(
                            candidate,
                            callbackUri,
                            clientState,
                            now,
                            cancellationToken)
                        : candidate.Subscription! with
                        {
                            LastMaintainedAtUtc = now,
                            LastMaintenanceFailureCode = null
                        };
                    await subscriptions.SaveAsync(maintained, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await subscriptions.RecordMaintenanceFailureAsync(
                        candidate.ApprovedMailboxId,
                        "graph_subscription_maintenance_failed",
                        now,
                        cancellationToken);
                }
            }
        }

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
