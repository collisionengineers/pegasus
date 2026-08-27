namespace Pegasus.Core.Identity;

public enum MailboxWakeKind
{
    Created,
    Missed,
    SubscriptionRemoved,
    ReauthorizationRequired
}

public interface IMailboxWakeEnqueuer
{
    Task EnqueueAsync(
        Guid approvedMailboxId,
        Guid subscriptionId,
        MailboxWakeKind wakeKind,
        CancellationToken cancellationToken);
}
