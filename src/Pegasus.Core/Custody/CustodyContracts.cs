using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

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
    string ETag,
    string? BoxVersionId = null);

/// <summary>
/// Fail-closed authority carried through a multi-effect custody adapter call.
/// The adapter invokes it immediately before each remote mutation.
/// </summary>
public sealed class CustodyEffectLeaseGuard(
    Func<CancellationToken, Task<bool>> holdsLease)
{
    public async Task RequireCurrentAsync(CancellationToken cancellationToken)
    {
        if (!await holdsLease(cancellationToken))
        {
            throw new CustodyProcessingLeaseLostException();
        }
    }
}

public sealed class CustodyProcessingLeaseLostException()
    : InvalidOperationException(
        "This custody processing attempt no longer owns an unexpired lease.");

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

    async Task<CaseCustodyRoot> CreateCaseRootAsync(
        Guid caseId,
        string caseReference,
        string creationOwnerToken,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        return await CreateCaseRootAsync(
            caseId, caseReference, creationOwnerToken, operationKey, cancellationToken);
    }

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

    async Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        return await RetainAcceptedIntakeSourceAsync(root, source, operationKey, cancellationToken);
    }

    /// <summary>
    /// Retains one attachment of the accepted instruction as its own file
    /// beside the retained source, at the given ordinal (the source is 1).
    /// Idempotent: an existing file must match the retained content exactly.
    /// Defaults fail closed for adapters that do not support attachment custody.
    /// </summary>
    Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference attachment,
        int ordinal,
        string operationKey,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This custody adapter does not retain instruction attachments.");

    async Task<CustodyDocumentVersion> RetainAcceptedIntakeAttachmentAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference attachment,
        int ordinal,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        return await RetainAcceptedIntakeAttachmentAsync(
            root, attachment, ordinal, operationKey, cancellationToken);
    }

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

    async Task<string> CreateAuditReferenceFolderAsync(
        CaseCustodyRoot root,
        string auditReference,
        string creationOwnerToken,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        return await CreateAuditReferenceFolderAsync(
            root, auditReference, creationOwnerToken, operationKey, cancellationToken);
    }

    /// <summary>
    /// Retains one group image inside an Image-initiated Case root created by
    /// <see cref="CreateCaseRootAsync(Guid, string, string, string, CancellationToken)"/>
    /// with the Image intake identity in the case slots. The stored name carries the
    /// group ordinal so ordering is stable and duplicate file names cannot collide.
    /// Idempotent: an existing file must match the retained content exactly.
    /// Defaults fail closed for adapters that do not support image-case custody.
    /// </summary>
    Task<CustodyDocumentVersion> RetainImageCaseAssetAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        int ordinal,
        string operationKey,
        CancellationToken cancellationToken) =>
        Task.FromException<CustodyDocumentVersion>(new NotSupportedException(
            "Image-case custody is not supported by this adapter."));

    async Task<CustodyDocumentVersion> RetainImageCaseAssetAsync(
        CaseCustodyRoot root,
        IntakeSourceCustodyReference source,
        int ordinal,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        return await RetainImageCaseAssetAsync(
            root, source, ordinal, operationKey, cancellationToken);
    }

    /// <summary>
    /// Folds a merged Image-initiated Case folder into the paired instruction
    /// case: every retained image moves into the case root's image evidence
    /// location and the emptied image-case folder is removed. Both roots must
    /// verify inside the approved custody root; anything unexpected left in the
    /// image-case folder fails the fold closed instead of being deleted.
    /// </summary>
    Task MergeImageCaseContentsAsync(
        CaseCustodyRoot imageRoot,
        CaseCustodyRoot caseRoot,
        string operationKey,
        CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException(
            "Image-case custody is not supported by this adapter."));

    async Task MergeImageCaseContentsAsync(
        CaseCustodyRoot imageRoot,
        CaseCustodyRoot caseRoot,
        string operationKey,
        CustodyEffectLeaseGuard leaseGuard,
        CancellationToken cancellationToken)
    {
        await leaseGuard.RequireCurrentAsync(cancellationToken);
        await MergeImageCaseContentsAsync(imageRoot, caseRoot, operationKey, cancellationToken);
    }
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

public sealed record CustodyRetryDecisionState(
    bool OperationExists,
    bool OperationMatches,
    long? OperationAfterVersion,
    bool CaseExists,
    long? CaseVersion,
    bool WorkExists,
    string? WorkState,
    bool AnotherRetryWon,
    long? WinningRetryVersion,
    bool CustodyAlreadyConfirmed,
    bool AuditReferenceExists);

/// <summary>
/// The sole owner of custody-retry replay, conflict, and eligibility decisions.
/// Persistence supplies a snapshot and applies only a Pending transition under CAS.
/// </summary>
public static class CustodyRetryPolicy
{
    public static RetryCaseCustodyResult Decide(CustodyRetryDecisionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.OperationExists)
        {
            return state.OperationMatches
                ? new(RetryCaseCustodyOutcome.Replay, state.OperationAfterVersion,
                    "The original custody retry request is already pending.")
                : new(RetryCaseCustodyOutcome.Conflict, null,
                    "The custody retry operation key was already used for another request.");
        }
        if (!state.CaseExists)
        {
            return new(RetryCaseCustodyOutcome.NotFound, null, "The case was not found.");
        }
        if (!state.WorkExists)
        {
            return new(RetryCaseCustodyOutcome.Refused, state.CaseVersion,
                "No matching custody work exists.");
        }
        if (!string.Equals(state.WorkState, "failed", StringComparison.Ordinal))
        {
            return state.AnotherRetryWon
                ? new(RetryCaseCustodyOutcome.Conflict, state.WinningRetryVersion,
                    "Another authorized retry already re-armed this custody work with a different operation key.")
                : new(RetryCaseCustodyOutcome.Refused, state.CaseVersion,
                    "Only failed custody work can be retried.");
        }
        if (state.CustodyAlreadyConfirmed)
        {
            return new(RetryCaseCustodyOutcome.Refused, state.CaseVersion,
                "Confirmed custody cannot be retried.");
        }
        if (!state.AuditReferenceExists)
        {
            return new(RetryCaseCustodyOutcome.Refused, state.CaseVersion,
                "The case has no immutable Audit reference to store.");
        }
        return new(RetryCaseCustodyOutcome.Pending, state.CaseVersion,
            "Custody retry queued.");
    }
}

public sealed class CustodyRetryPolicyAuthority
{
    private readonly Func<CustodyRetryDecisionState, RetryCaseCustodyResult> decide;

    private CustodyRetryPolicyAuthority() => decide = CustodyRetryPolicy.Decide;

    public static CustodyRetryPolicyAuthority Core { get; } = new();

    public RetryCaseCustodyResult Decide(CustodyRetryDecisionState state) =>
        decide(state);
}

public interface ICaseCustodyQueries
{
    Task<IReadOnlyList<CaseCustodyPreparation>> GetPreparationsAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);
}

public interface ICustodyRecoveryPersistence
{
    Task<RetryCaseCustodyResult> RetryAsync(
        RetryCaseCustodyRequest request,
        string normalizedReason,
        string requestHash,
        CustodyRetryPolicyAuthority policy,
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
public sealed class RetryCaseCustody(ICustodyRecoveryPersistence persistence) : IRetryCaseCustody
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
        return await persistence.RetryAsync(
            request with
            {
                OperationKey = operationKey,
                Reason = reason,
                EditLeaseToken = leaseToken
            },
            reason,
            requestHash,
            CustodyRetryPolicyAuthority.Core,
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

    /// <summary>
    /// Claims one known committed work item for publication without scanning
    /// the durable outbox.
    /// </summary>
    Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
        Guid workItemId,
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

/// <summary>
/// The post-commit external/custody publication boundary. Composition must
/// provide it so accepted work cannot silently await a broad recovery scan.
/// </summary>
public interface ICommittedExternalWorkPublisher
{
    Task PublishAsync(Guid workItemId, CancellationToken cancellationToken);
}

public sealed class DispatchPendingExternalWork(
    IExternalWorkStore workStore,
    IExternalWorkEnqueuer workEnqueuer,
    TimeProvider timeProvider) : ICommittedExternalWorkPublisher
{
    private static readonly TimeSpan DispatchLeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailedDispatchDelay = TimeSpan.FromSeconds(30);
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Custody");

    /// <summary>
    /// Best-effort publication after the transaction that created this work
    /// has committed. Transport failure remains recoverable and never undoes
    /// the already-created case or custody state.
    /// </summary>
    public async Task ExecuteCommittedAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("An external work item identifier is required.", nameof(workItemId));
        }

        using var activity = Telemetry.StartActivity("publish_committed_external_work");
        activity?.SetTag("custody.work_item_id", workItemId);
        activity?.SetTag("custody.publication.path", "immediate");

        var claim = await workStore.ClaimDispatchAsync(
            workItemId,
            timeProvider.GetUtcNow(),
            DispatchLeaseDuration,
            cancellationToken);
        if (claim is null)
        {
            activity?.SetTag("custody.publication.outcome", "already_claimed_or_complete");
            return;
        }

        try
        {
            await workEnqueuer.EnqueueAsync(claim.WorkItemId, cancellationToken);
            await workStore.MarkDispatchedAsync(
                claim.WorkItemId,
                claim.LeaseToken,
                timeProvider.GetUtcNow(),
                cancellationToken);
            activity?.SetTag("custody.publication.outcome", "published");
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("custody.publication.enqueue_error", exception.GetType().Name);
            try
            {
                await workStore.ReleaseDispatchAsync(
                    claim.WorkItemId,
                    claim.LeaseToken,
                    timeProvider.GetUtcNow(),
                    CancellationToken.None);
                activity?.SetTag("custody.publication.outcome", "enqueue_failed_released");
            }
            catch (Exception releaseException) when (IntakeExceptionPolicy.IsRecoverable(releaseException))
            {
                activity?.SetTag("custody.publication.release_error", releaseException.GetType().Name);
                activity?.SetTag("custody.publication.outcome", "enqueue_failed_lease_expiry_recovery");
            }

            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        }
    }

    public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken) =>
        ExecuteCommittedAsync(workItemId, cancellationToken);

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

public enum CaseArtifactCustodyDisposition { Confirmed, Pending, Failed, Unknown }
public sealed record CaseArtifactCustodyRequest(
    ActionActor Actor, Guid? CaseId, Guid? IntakeReceiptId, string OccurrenceIdentity,
    string OperationKey, string FileName, string MediaType, long ContentLength,
    string Sha256, Stream Content);
public sealed record CaseArtifactCustodyResult(
    CaseArtifactCustodyDisposition Disposition, Guid? DocumentId, Guid? VersionId,
    string? BoxFileId, string? BoxVersionId, string? Sha256, long? ContentLength,
    string? MediaType, string? FailureCode, string? PendingContentStorageKey);
public interface ICaseArtifactCustody
{
    Task<CaseArtifactCustodyResult> RetainAsync(
        CaseArtifactCustodyRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the durable state of an exact Case artifact version. A pending result
/// retains its logical identities so callers can retry after process restart.
/// This is custody state, not report readiness or permission to send.
/// </summary>
public interface ICaseArtifactCustodyStatus
{
    Task<CaseArtifactCustodyResult> GetAsync(
        ActionActor actor, Guid caseId, Guid documentId, Guid versionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds an accepted intent after a response was lost, without offering
    /// content again. Null means no committed intent was observed; it does not
    /// authorize a new operation key or prove an in-flight call cannot commit.
    /// Request links may read only intents accepted through that exact link.
    /// </summary>
    Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
        ActionActor actor, Guid caseId, string operationKey,
        CancellationToken cancellationToken);
}
