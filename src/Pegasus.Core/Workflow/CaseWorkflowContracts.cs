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
    Active,
    ReportPreparation,
    PostReport,
    PostReportComplete,
    ProviderCancelled,
    CollisionEngineersRejected,
    CreatedInError
}

public enum CaseClosureOutcome
{
    PostReportComplete,
    ProviderCancelled,
    CollisionEngineersRejected,
    CreatedInError
}

public enum CaseReopenDestination
{
    NotReady,
    Review,
    Active,
    ReportPreparation,
    PostReport
}

public sealed record CaseWorkflowConfiguration(
    bool RequireCompleteInstructionsBeforeEngineerAssignment,
    bool RequireCompleteImagesBeforeEngineerAssignment,
    bool RequireStaffInstructionReviewBeforeEngineerAssignment,
    bool RequireStaffImageReviewBeforeEngineerAssignment,
    string PolicyKey,
    int PolicyVersion);

public interface ICaseWorkflowConfiguration
{
    CaseWorkflowConfiguration GetCurrent();
}

public sealed record CaseReadinessEvidence(
    bool InstructionsComplete,
    bool ImagesComplete,
    bool InstructionsReviewedByStaff,
    bool ImagesReviewedByStaff,
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
/// Exact retained approved-mailbox Sent evidence. A caller cannot substitute a draft,
/// manual assertion, queue result, prepared text, or a report file for this evidence.
/// </summary>
public sealed record ApprovedMailboxReportSentEvidence(
    Guid EvidenceId,
    string MailboxIdentity,
    string SentFolderIdentity,
    string ImmutableItemIdentity,
    string ConversationIdentity,
    string ReplyChainIdentity,
    DateTimeOffset SentAtUtc,
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
    long Version);

public sealed record CaseEditLease(
    Guid CaseId,
    string Token,
    string Holder,
    long Version,
    DateTimeOffset ExpiresAtUtc);

public sealed class CaseVersionConflictException(Guid caseId, long expectedVersion, long actualVersion)
    : InvalidOperationException($"Case '{caseId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid CaseId { get; } = caseId;

    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}

public sealed class CaseEditLeaseConflictException(Guid caseId)
    : InvalidOperationException($"Case '{caseId}' is currently being edited by another actor.")
{
    public Guid CaseId { get; } = caseId;
}

public sealed class CaseEditLeaseExpiredException(Guid caseId)
    : InvalidOperationException($"The edit lease for case '{caseId}' is no longer valid.")
{
    public Guid CaseId { get; } = caseId;
}

public sealed class CaseOperationConflictException(Guid caseId, string operationKey)
    : InvalidOperationException($"Operation '{operationKey}' was already applied to case '{caseId}' with different inputs.")
{
    public Guid CaseId { get; } = caseId;

    public string OperationKey { get; } = operationKey;
}

public sealed record ClaimCaseEditLeaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

public sealed record RenewCaseEditLeaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string LeaseToken);

public sealed record ReleaseCaseEditLeaseRequest(
    Guid CaseId,
    ActionActor Actor,
    string LeaseToken);

public abstract record CaseMutationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken);

public sealed record PutCaseOnHoldRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    DateTimeOffset HeldAtUtc)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record ReturnCaseToReviewRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseReadinessEvidence Readiness)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record AssignCaseEngineerRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    Guid EngineerId,
    CaseReadinessEvidence Readiness)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record RecordCaseReportApprovalRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    ReportApprovalEvidence Approval)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record RecordCaseReportSentRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    ApprovedMailboxReportSentEvidence Evidence)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record CloseCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseClosureOutcome Outcome,
    Guid? ReplacementCaseId = null)
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
}

public interface ILeaseCaseForEdit
{
    Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken);

    Task<CaseEditLease> RenewAsync(RenewCaseEditLeaseRequest request, CancellationToken cancellationToken);

    Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken);
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
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> RecordReportApprovalAsync(
        RecordCaseReportApprovalRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> RecordReportSentAsync(
        RecordCaseReportSentRequest request,
        CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> CloseAsync(CloseCaseRequest request, CancellationToken cancellationToken);

    Task<CaseWorkflowRecord> ReopenAsync(ReopenCaseRequest request, CancellationToken cancellationToken);
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

public interface IStartCaseWork
{
    Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken);
}

public interface IBeginCaseReportPreparation
{
    Task<CaseWorkflowRecord> ExecuteAsync(CaseMutationRequest request, CancellationToken cancellationToken);
}

public interface IRecordCaseReportApproval
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        RecordCaseReportApprovalRequest request,
        CancellationToken cancellationToken);
}

public interface IRecordCaseReportSent
{
    Task<CaseWorkflowRecord> ExecuteAsync(
        RecordCaseReportSentRequest request,
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
