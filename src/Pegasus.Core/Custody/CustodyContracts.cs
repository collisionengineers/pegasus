using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Identity;

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
    string SourceObjectKey,
    long SourceLength = -1);

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
        CancellationToken cancellationToken) => CreateCaseRootAsync(
            caseId,
            caseReference,
            CustodyCreationOwner.Create(),
            operationKey,
            cancellationToken);

    Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
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
        CancellationToken cancellationToken) => CreateAuditReferenceFolderAsync(
            root,
            auditReference,
            CustodyCreationOwner.Create(),
            operationKey,
            cancellationToken);

    Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CancellationToken cancellationToken);
}

public enum CustodyTargetKind
{
    CaseSource,
    AuditReference
}

public static class CustodyCreationOwner
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        Span<char> result = stackalloc char[26];
        var buffer = 0;
        var bits = 0;
        var output = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                result[output++] = Alphabet[(buffer >> bits) & 31];
            }
        }
        if (bits > 0)
        {
            result[output++] = Alphabet[(buffer << (5 - bits)) & 31];
        }
        if (output != result.Length)
        {
            throw new InvalidOperationException("The custody creation owner could not be encoded.");
        }
        return new string(result);
    }
}

public sealed record CaseCustodyPreparation(
    Guid CaseId,
    long CaseVersion,
    CustodyTargetKind TargetKind,
    string State,
    string? SafeFailureReason,
    int AttemptCount,
    bool CanRetry);

public sealed record RetryCaseCustodyRequest(
    Guid CaseId,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CustodyTargetKind TargetKind);

public enum RetryCaseCustodyOutcome
{
    Pending,
    Replay,
    Conflict,
    Refused,
    NotFound
}

public sealed record RetryCaseCustodyResult(
    RetryCaseCustodyOutcome Outcome,
    long? CaseVersion,
    string Message);

public interface ICaseCustodyQueries
{
    Task<IReadOnlyList<CaseCustodyPreparation>> GetPreparationsAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);
}

public interface ICustodyRecoveryStore
{
    Task<RetryCaseCustodyResult> RetryAsync(
        RetryCaseCustodyRequest request,
        string normalizedReason,
        string requestHash,
        CancellationToken cancellationToken);
}

public interface IRetryCaseCustody
{
    Task<RetryCaseCustodyResult> ExecuteAsync(
        RetryCaseCustodyRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The one business boundary that permits a persisted custody failure to be
/// re-armed. Queue redelivery and source replay never enter this use case.
/// </summary>
public sealed class RetryCaseCustody(ICustodyRecoveryStore store) : IRetryCaseCustody
{
    public async Task<RetryCaseCustodyResult> ExecuteAsync(
        RetryCaseCustodyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.CaseId == Guid.Empty || request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentException("A current Case and rendered workflow version are required.", nameof(request));
        }
        if (!Enum.IsDefined(request.TargetKind))
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        var reason = RequireText(request.Reason, 500, nameof(request.Reason));
        var operationKey = RequireText(request.OperationKey, 100, nameof(request.OperationKey));
        var leaseToken = RequireText(request.EditLeaseToken, 200, nameof(request.EditLeaseToken));
        var material = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            request.CaseId,
            request.ExpectedCaseVersion,
            targetKind = request.TargetKind.ToString(),
            actorKind = request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            roles = request.Actor.Roles.OrderBy(value => value).Select(value => value.ToString()).ToArray(),
            operationKey,
            reason,
            leaseToken
        });
        var requestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();
        return await store.RetryAsync(
            request with
            {
                OperationKey = operationKey,
                Reason = reason,
                EditLeaseToken = leaseToken
            },
            reason,
            requestHash,
            cancellationToken);
    }

    private static string RequireText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }
        var normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The value is invalid.", parameterName);
        }
        return normalized;
    }
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

    Task<bool> HoldsProcessingLeaseAsync(
        Guid workItemId,
        string leaseToken,
        CancellationToken cancellationToken);

    Task FailProcessingAsync(
        Guid workItemId,
        string leaseToken,
        DateTimeOffset failedAtUtc,
        string failureCode,
        string failureReason,
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
