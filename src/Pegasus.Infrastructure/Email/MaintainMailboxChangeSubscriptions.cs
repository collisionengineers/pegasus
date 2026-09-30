using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// One pass of Microsoft Graph change-subscription maintenance over the approved
/// mailbox estate: create a missing subscription, renew one that is inside its
/// renewal window, and record a failure against the one mailbox without stopping
/// the pass. The Worker runs it before the approved-inbox fallback poll. Without a
/// Graph adapter (the offline profile) it does nothing.
/// </summary>
/// <remarks>
/// The notification address and client state are read when the pass starts, not
/// when the class is built, so a missing value fails this pass and nothing else.
/// </remarks>
public sealed partial class MaintainMailboxChangeSubscriptions(
    IApprovedMailboxSubscriptionStore subscriptions,
    IEnumerable<GraphMailboxChangeSubscriptions> graphSubscriptionProviders,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<MaintainMailboxChangeSubscriptions> logger)
{
    private static readonly TimeSpan RenewalWindow = TimeSpan.FromHours(48);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var graphSubscriptions = graphSubscriptionProviders.SingleOrDefault();
        if (graphSubscriptions is null)
        {
            return;
        }

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
                    now.Add(RenewalWindow))
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
                await subscriptions.SaveAsync(
                    maintained,
                    candidate.Subscription?.SubscriptionId,
                    cancellationToken);
            }
            catch (ApprovedMailboxSubscriptionMaintenanceLostException)
            {
                LogSubscriptionMaintenanceDeferred(logger, candidate.ApprovedMailboxId, candidate.Generation);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                try
                {
                    await subscriptions.RecordMaintenanceFailureAsync(
                        candidate.ApprovedMailboxId,
                        candidate.Generation,
                        candidate.Subscription?.SubscriptionId,
                        "graph_subscription_maintenance_failed",
                        now,
                        cancellationToken);
                }
                catch (ApprovedMailboxSubscriptionMaintenanceLostException)
                {
                    LogSubscriptionMaintenanceDeferred(logger, candidate.ApprovedMailboxId, candidate.Generation);
                }
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Deferred obsolete subscription maintenance for mailbox {MailboxId}, generation {Generation}.")]
    private static partial void LogSubscriptionMaintenanceDeferred(
        ILogger logger, Guid mailboxId, long generation);
}
