namespace Pegasus.Core.Custody;

public enum CustodyWorkKind
{
    CreateCaseRoot,
    RetainAcceptedIntakeSource,
    CreateAuditReferenceFolder
}

public sealed record CustodyWork(
    Guid Id,
    CustodyWorkKind Kind,
    Guid CaseId,
    string OperationKey);

public sealed record CaseCustodyRoot(
    Guid CaseId,
    string RemoteId,
    string Reference);

public sealed record IntakeSourceCustodyReference(
    Guid IntakeReceiptId,
    string SourceFileName,
    string MediaType,
    string SourceHash,
    string SourceObjectKey);

public sealed record CustodyDocumentVersion(
    Guid CaseId,
    string RemoteId,
    string ContentHash,
    string ETag);

/// <summary>
/// A case-scoped port. Implementations must guard the configured custody root and never accept an
/// arbitrary remote identifier from a caller.
/// </summary>
public interface ICaseCustody
{
    Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the immutable custody root already allocated for the case. This read does not
    /// create or relabel a root and must validate the retained case identity.
    /// </summary>
    Task<CaseCustodyRoot> GetExistingCaseRootAsync(
        Guid caseId,
        string caseReference,
        CancellationToken cancellationToken);

    Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CancellationToken cancellationToken);

    Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string operationKey,
        CancellationToken cancellationToken);
}

public sealed class CaseCustodyUnavailableException()
    : InvalidOperationException(
        "Case custody is unavailable until an approved production adapter is configured.")
{
}

public sealed record ExternalWorkDispatchClaim(
    Guid WorkItemId,
    string LeaseToken);

/// <summary>
/// Owns durable dispatch leasing for committed external work. A successful claim must be
/// persisted before it is returned, and an expired claim must be available for safe replay.
/// </summary>
public interface IExternalWorkStore
{
    Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task MarkDispatchedAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dispatchedAtUtc,
        CancellationToken cancellationToken);

    Task ReleaseDispatchAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        CancellationToken cancellationToken);

    Task MarkPoisonedAsync(
        Guid workItemId,
        DateTimeOffset failedAtUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// Publishes only the stable persisted work identifier. Delivery is at least once.
/// </summary>
public interface IExternalWorkEnqueuer
{
    Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken);
}

public sealed class DispatchPendingExternalWork(
    IExternalWorkStore workStore,
    IExternalWorkEnqueuer workEnqueuer,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan DispatchLeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailedDispatchDelay = TimeSpan.FromSeconds(30);

    public async Task<int> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var dispatched = 0;
        while (dispatched < maximumItems)
        {
            var claim = await workStore.ClaimDispatchAsync(
                timeProvider.GetUtcNow(),
                DispatchLeaseDuration,
                cancellationToken);
            if (claim is null)
            {
                break;
            }

            if (claim.WorkItemId == Guid.Empty || string.IsNullOrWhiteSpace(claim.LeaseToken))
            {
                throw new InvalidOperationException(
                    "A claimed external work item must have an identifier and lease token.");
            }

            try
            {
                await workEnqueuer.EnqueueAsync(claim.WorkItemId, cancellationToken);
                await workStore.MarkDispatchedAsync(
                    claim.WorkItemId,
                    claim.LeaseToken,
                    timeProvider.GetUtcNow(),
                    cancellationToken);
            }
            catch
            {
                await workStore.ReleaseDispatchAsync(
                    claim.WorkItemId,
                    claim.LeaseToken,
                    timeProvider.GetUtcNow().Add(FailedDispatchDelay),
                    CancellationToken.None);
                throw;
            }

            dispatched++;
        }

        return dispatched;
    }
}

public sealed class ReconcilePoisonedExternalWork(
    IExternalWorkStore workStore,
    TimeProvider timeProvider)
{
    public Task ExecuteAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An external work item identifier is required.",
                nameof(workItemId));
        }

        return workStore.MarkPoisonedAsync(
            workItemId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }
}

public interface IProcessQueuedCustody
{
    Task ExecuteAsync(Guid workId, CancellationToken cancellationToken);
}
