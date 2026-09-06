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
    string? LastMaintenanceFailureCode,
    long Generation = 0);

public sealed record ApprovedMailboxSubscriptionMaintenanceCandidate(
    Guid ApprovedMailboxId,
    string GraphMailboxId,
    string InboxFolderIdentity,
    ApprovedMailboxSubscription? Subscription,
    long Generation = 0);

public sealed class ApprovedMailboxSubscriptionMaintenanceLostException : Exception
{
    public ApprovedMailboxSubscriptionMaintenanceLostException()
        : base("The approved mailbox subscription maintenance attempt is no longer current.")
    {
    }
}

public interface IApprovedMailboxSubscriptionStore
{
    /// <summary>
    /// Every subscription row, one per approved mailbox, for the administration surface
    /// to report. Read-only there: Web holds SELECT alone on the table.
    /// </summary>
    Task<IReadOnlyList<ApprovedMailboxSubscription>> ListAsync(
        CancellationToken cancellationToken);

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
        string? expectedPriorSubscriptionId,
        CancellationToken cancellationToken);

    Task RecordMaintenanceFailureAsync(
        Guid approvedMailboxId,
        long expectedGeneration,
        string? expectedSubscriptionId,
        string failureCode,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken);
}
