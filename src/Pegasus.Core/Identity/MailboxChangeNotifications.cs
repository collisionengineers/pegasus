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
        long generation,
        MailboxWakeKind wakeKind,
        string? immutableMessageId,
        CancellationToken cancellationToken);
}
