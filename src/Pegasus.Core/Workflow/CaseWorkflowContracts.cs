using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The editable state of an accepted case. Terminal outcomes are states rather than a
/// generic "closed" flag so that projections and history cannot lose the selected outcome.
/// </summary>
public enum CaseLifecycleState
{
    NotReady,
    Held,
    Review,
    ReportPreparation,
    PostReport,
    PostReportComplete,
    ProviderCancelled,
    CollisionEngineersRejected,
    CreatedInError,
    SourceEmailUnlinked
}

public enum CaseClosureOutcome
{
    PostReportComplete,
    ProviderCancelled,
    CollisionEngineersRejected,
    CreatedInError,
    SourceEmailUnlinked
}

public enum CaseReopenDestination
{
    NotReady,
    Review,
    ReportPreparation,
    PostReport
}

public sealed record CaseWorkflowConfiguration(
    string PolicyKey,
    int PolicyVersion);

public interface ICaseWorkflowConfiguration
{
    Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken);
}

/// <summary>
/// CASE-046: retained only because the Case pages still post it. No gate reads
/// it: entry to Review is decided by the persisted completeness facts the
/// transition store re-reads inside its own transaction, so nothing a client
/// sends here is evidence of anything. Delete this record with the last posting
/// caller.
/// </summary>
public sealed record CaseReadinessEvidence(
    bool InstructionsComplete,
    bool ImagesComplete,
    string EvidenceReference);

/// <summary>
/// A human approval of one immutable report artifact. It does not claim the report was sent.
/// </summary>
public sealed record ReportApprovalEvidence(
    Guid ApprovalId,
    string ArtifactIdentity,
    string ArtifactSha256,
    ActionActor ApprovedBy,
    DateTimeOffset ApprovedAtUtc);

/// <summary>
/// Caller-supplied identity of the immutable report artifact being approved. The approving
/// actor and approval time are assigned by the authenticated mutation boundary.
/// </summary>
public sealed record ReportApprovalSubmission(
    Guid ApprovalId,
    string ArtifactIdentity,
    string ArtifactSha256);

/// <summary>
/// Exact retained approved-mailbox Sent evidence. A caller cannot substitute a draft,
/// manual assertion, queue result, prepared text, or a report file for this evidence.
/// </summary>
public sealed record ApprovedMailboxReportSentEvidence(
    Guid EvidenceId,
    string MailboxIdentity,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string InternetMessageIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    string SourceOccurrenceIdentity,
    string SourceSha256,
    string MimeSha256,
    DateTimeOffset SentAtUtc,
    DateTimeOffset DiscoveredAtUtc,
    ActionActor DiscoveredBy,
    DateTimeOffset LinkedAtUtc,
    ActionActor LinkedBy);

public sealed record CaseWorkflowRecord(
    Guid CaseId,
    CaseIdentity Identity,
    CaseLifecycleState State,
    Guid? AssignedEngineerId,
    ReportApprovalEvidence? ReportApproval,
    ApprovedMailboxReportSentEvidence? ReportSentEvidence,
    CaseDueWork? DueWork,
    CaseClosureOutcome? ClosureOutcome,
    Guid? OriginalCaseId,
    Guid? ReplacementCaseId,
    long Version)
{
    public CaseArchive? Archive { get; init; }

    public Guid? SignOffEngineerId { get; init; }
}

public sealed record CaseEditLease(
    Guid CaseId,
    string Token,
    string Holder,
    long Version,
    DateTimeOffset ExpiresAtUtc)
{
    public long Generation { get; init; }
}

public sealed class CaseVersionConflictException(Guid caseId, long expectedVersion, long actualVersion)
    : InvalidOperationException($"Case '{caseId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid CaseId { get; } = caseId;

    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}

public sealed class CaseEditLeaseConflictException(Guid caseId, long caseVersion)
    : InvalidOperationException($"Case '{caseId}' is currently being edited by another actor.")
{
    public Guid CaseId { get; } = caseId;

    public long CaseVersion { get; } = caseVersion;
}

public sealed class CaseEditLeaseExpiredException(Guid caseId, long caseVersion)
    : InvalidOperationException($"The edit lease for case '{caseId}' is no longer valid.")
{
    public Guid CaseId { get; } = caseId;

    public long CaseVersion { get; } = caseVersion;
}

/// <summary>
/// CASE-046: a Review-gated transition was attempted while the case's own
/// persisted completeness facts say it is not ready. The facts are re-read by
/// the transition store inside its own transaction, so this is never a report
/// of what a client claimed.
/// </summary>
public sealed class CaseReviewReadinessException(Guid caseId)
    : InvalidOperationException(
        $"Case '{caseId}' requires complete instructions and images before Review.")
{
    public Guid CaseId { get; } = caseId;
}

public sealed class CaseOperationConflictException(Guid caseId, string operationKey)
    : InvalidOperationException($"Operation '{operationKey}' was already applied to case '{caseId}' with different inputs.")
{
    public Guid CaseId { get; } = caseId;

    public string OperationKey { get; } = operationKey;
}

/// <summary>
/// Claims one short-lived edit lease. Within a case, the normalized operation key identifies this
/// exact request, including the expected version and the complete authorized actor identity.
/// </summary>
public sealed record ClaimCaseEditLeaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

public sealed record RenewCaseEditLeaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string LeaseToken);

/// <summary>
/// Extends the lease the caller already holds, so an open editor is never timed
/// out mid-edit. It carries no operation key and no expected version: a version
/// cannot move under a live lease, because every mutation clears the lease as it
/// commits, so demanding one would only invent a failure the holder cannot fix.
/// </summary>
public sealed record HeartbeatCaseEditLeaseRequest(
    Guid CaseId,
    ActionActor Actor,
    string LeaseToken);

public sealed record ReleaseCaseEditLeaseRequest(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    string LeaseToken);

public sealed record ClearCaseEditLeaseRequest(
    Guid CaseId,
    Guid ExpectedHolderUserId,
    long ExpectedLeaseGeneration,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record ClearCaseEditLeaseResult(
    Guid CaseId,
    Guid HolderUserId,
    long LeaseGeneration,
    long CaseVersion,
    DateTimeOffset ClearedAtUtc);

public abstract record CaseMutationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public sealed record ChangeCaseStateRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record PutCaseOnHoldRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record ReturnCaseToReviewRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseReadinessEvidence? Readiness = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record AssignCaseEngineerRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EngineerId,
    CaseReadinessEvidence? Readiness = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record SetCaseSignOffEngineerRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid SignOffEngineerId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record RecordCaseReportApprovalRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    ReportApprovalSubmission Approval)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record LinkReportEvidenceRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EvidenceId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

/// <summary>
/// System-worker request to associate retained exact approved-mailbox Sent evidence when
/// the polling policy supplied one unambiguous authoritative Case identity.
/// </summary>
public sealed record AutoLinkReportEvidenceRequest(
    Guid CaseId,
    Guid EvidenceId,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public enum AutoLinkReportEvidenceDisposition
{
    Linked,
    NotLinked
}

/// <summary>
/// Minimal committed association returned to the Worker without exposing the broader
/// case projection or requiring unrelated Case/Principal reads.
/// </summary>
public sealed record AutoLinkedReportEvidence(
    Guid CaseId,
    Guid EvidenceId,
    CaseLifecycleState State,
    long Version);

/// <summary>
/// A policy denial or a concurrent staff change is a retained, visible non-link rather
/// than an overwrite. Link is present only for the canonical committed/replayed association.
/// </summary>
public sealed record AutoLinkReportEvidenceResult(
    AutoLinkReportEvidenceDisposition Disposition,
    AutoLinkedReportEvidence? Link,
    string? NotLinkedReasonCode);

public sealed record UnlinkReportEvidenceRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EvidenceId)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record CloseCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseClosureOutcome Outcome)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record ReopenCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseReopenDestination Destination,
    CaseReadinessEvidence? Readiness = null)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ICaseWorkflowQueries
{
    Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken);

    Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken);
}

/// <summary>
/// Atomic persistence boundary for case edit leases. An exact claim or renewal replay returns the
/// same opaque token and expiry, and an exact release replay returns success, before mutable-state,
/// version, ownership, or expiry preconditions are evaluated. Reusing an operation key with
/// different request material fails with <see cref="CaseOperationConflictException"/>.
/// Actor authorization always precedes replay recovery.
/// </summary>
public interface ILeaseCaseForEdit
{
    Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken);

    Task<CaseEditLease> RenewAsync(RenewCaseEditLeaseRequest request, CancellationToken cancellationToken);

    Task<CaseEditLease> HeartbeatAsync(
        HeartbeatCaseEditLeaseRequest request,
        CancellationToken cancellationToken);

    Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken);
}

public interface IAdministrativeCaseEditLeaseStore
{
    Task<ClearCaseEditLeaseResult> ClearAsync(
        ClearCaseEditLeaseRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Persistence port for all case workflow mutations. Each operation is one atomic transaction:
/// optimistic-version and lease checks, case/due-work change, exact evidence link where supplied,
/// idempotency, and permanent action history either all commit or all fail.
/// </summary>
public interface ICaseWorkflowStore : ICaseWorkflowQueries, ILeaseCaseForEdit
{
    Task<CaseWorkflowRecord> ChangeStateAsync(
        CaseMutationRequest request,
        CaseLifecycleState targetState,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> HoldAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> ReleaseHoldAsync(
        CaseMutationRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> ReturnToReviewAsync(
        ReturnCaseToReviewRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> AssignEngineerAsync(
        AssignCaseEngineerRequest request,
        Guid? signOffEngineerId,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> SetSignOffEngineerAsync(
        SetCaseSignOffEngineerRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> RecordReportApprovalAsync(
        RecordCaseReportApprovalRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> LinkReportEvidenceAsync(
        LinkReportEvidenceRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> UnlinkReportEvidenceAsync(
        UnlinkReportEvidenceRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Transactional persistence boundary for the system-worker auto-link path. It shares the
/// staff link's evidence and chronology guards without requiring a staff lease; a successful
/// versioned mutation invalidates any active lease so concurrent staff work cannot overwrite it.
/// </summary>
public interface IAutoLinkReportEvidenceStore
{
    Task<AutoLinkReportEvidenceResult> TryAutoLinkAsync(
        AutoLinkReportEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IPutCaseOnHold
{
    Task<CaseWorkflowRecord> ExecuteAsync(PutCaseOnHoldRequest request, CancellationToken cancellationToken);
}

public interface IReleaseCaseHold
{
    Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken);
}

public interface IReturnCaseToReview
{
    Task<CaseWorkflowRecord> ExecuteAsync(ReturnCaseToReviewRequest request, CancellationToken cancellationToken);
}

public interface IAssignCaseEngineer
{
    Task<CaseWorkflowRecord> ExecuteAsync(AssignCaseEngineerRequest request, CancellationToken cancellationToken);
}

public interface ISetCaseSignOffEngineer
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        SetCaseSignOffEngineerRequest request,
        CancellationToken cancellationToken);
}

public interface IStartCaseWork
{
    Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken);
}


public interface IRecordCaseReportApproval
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        RecordCaseReportApprovalRequest request,
        CancellationToken cancellationToken);
}

public interface ILinkReportEvidence
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        LinkReportEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IAutoLinkReportEvidence
{
    Task<AutoLinkReportEvidenceResult> ExecuteAsync(
        AutoLinkReportEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IUnlinkReportEvidence
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        UnlinkReportEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface ICloseCase
{
    Task<CaseWorkflowRecord> ExecuteAsync(CloseCaseRequest request, CancellationToken cancellationToken);
}

public interface IReopenCase
{
    Task<CaseWorkflowRecord> ExecuteAsync(ReopenCaseRequest request, CancellationToken cancellationToken);
}
