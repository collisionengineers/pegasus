namespace Pegasus.Core.Identity;

public enum ApprovedMailboxSubscriptionLifecycleState
{
    Active,
    Missed,
    Removed,
    ReauthorizationRequired
}

public sealed record ApprovedMailboxSubscription(
    Guid ApprovedMailboxId,
    string SubscriptionId,
    string Resource,
    DateTimeOffset ExpiresAtUtc,
    ApprovedMailboxSubscriptionLifecycleState LifecycleState,
    DateTimeOffset? LastMaintainedAtUtc,
    string? LastMaintenanceFailureCode);

public sealed record ApprovedMailboxSubscriptionMaintenanceCandidate(
    Guid ApprovedMailboxId,
    string GraphMailboxId,
    string InboxFolderIdentity,
    ApprovedMailboxSubscription? Subscription);

public interface IApprovedMailboxSubscriptionStore
{
    Task<ApprovedMailboxSubscription?> GetActiveAsync(
        string subscriptionId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>
        ListMaintenanceCandidatesAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task SaveAsync(
        ApprovedMailboxSubscription subscription,
        CancellationToken cancellationToken);

    Task RecordMaintenanceFailureAsync(
        Guid approvedMailboxId,
        string failureCode,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken);
}
