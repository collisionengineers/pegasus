using Pegasus.Core.Triage;
using Pegasus.Core.Access;
using Pegasus.Core.ActionHistory;

namespace Pegasus.Core.Cases;

public enum CaseType
{
    Inspection,
    StandaloneAudit,
    InspectionAndAudit
}

public enum CaseWorkflowState
{
    NotReady,
    Review,
    ReportPreparation,
    PostReport
}

public enum CaseTerminalOutcome
{
    PostReportComplete,
    ProviderCancelled,
    CollisionEngineersRejected,
    CreatedInError
}
#pragma warning disable CA1711

public enum CaseQueue
{
    NotReady,
    Review,
    Held,
    DueToday,
    InToday,
    SentToEngineer,
    ReportsSent
}
#pragma warning restore CA1711

public enum CaseCommandFailure
{
    NotFound,
    Denied,
    AcceptanceIncomplete,
    UnknownPrincipal,
    SequenceExhausted,
    InvalidState,
    ReasonRequired,
    LeaseRequired,
    LeaseExpired,
    LeaseWrongHolder,
    StaleVersion,
    CreatedInErrorCannotReopen,
    ReplacementRequired,
    ExternalEvidenceUnavailable
}

public sealed record CaseIdentity(
    Guid Id,
    string PrincipalCode,
    string BaseReference,
    string DisplayReference,
    CaseType Type,
    string Registration,
    string? SecondaryAuditReference);

public sealed record CaseDueWork(DateTimeOffset? NextDueAtUtc, bool IsOverdue, int ChaseCount, bool IsPaused);
public sealed record CaseLeaseState(Guid? HolderId, string? HolderName, DateTimeOffset? ExpiresAtUtc, bool HeldByCurrentActor);
public sealed record CaseReplacementLink(Guid CaseId, string Reference, DateTimeOffset LinkedAtUtc, string Reason);
public sealed record CaseSourceLink(Guid SourceId, string Label);
public sealed record CaseHistoryEntry(Guid Id, string Action, string Outcome, DateTimeOffset OccurredAtUtc, string ActorName, string? Reason);

public sealed record CaseSummary(
    Guid Id,
    string DisplayReference,
    string Registration,
    string? Claimant,
    string? ClaimNumber,
    string PrincipalCode,
    CaseWorkflowState State,
    bool IsHeld,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? InstructionDate,
    string? EngineerName,
    CaseDueWork DueWork,
    long Version);

public sealed record CaseDetail(
    CaseIdentity Identity,
    string? Claimant,
    string? ClaimNumber,
    DateTimeOffset ReceivedAtUtc,
    DateOnly? InstructionDate,
    string? Origin,
    IReadOnlyList<CaseSourceLink> Sources,
    CaseWorkflowState State,
    bool IsHeld,
    CaseDueWork DueWork,
    Guid? EngineerId,
    string? EngineerName,
    CaseTerminalOutcome? TerminalOutcome,
    CaseReplacementLink? Replacement,
    CaseLeaseState Lease,
    long Version,
    IReadOnlyList<CaseHistoryEntry> History);

public sealed record CaseQuery(
    string? CaseReference = null,
    string? Registration = null,
    string? Claimant = null,
    string? ClaimNumber = null,
    string? PrincipalCode = null,
    CaseWorkflowState? State = null,
    Guid? EngineerId = null,
    DateOnly? ReceivedFrom = null,
    DateOnly? ReceivedTo = null,
    DateOnly? InstructionFrom = null,
    DateOnly? InstructionTo = null,
    string? Origin = null,
    CaseQueue? Queue = null,
    int Page = 1,
    int PageSize = 50);

public sealed record CaseCommandResult(CaseDetail? Detail, CaseCommandFailure? Failure, string? Message = null)
{
    public bool Succeeded => Detail is not null && Failure is null;
    public static CaseCommandResult Failed(CaseCommandFailure failure, string? message = null) => new(null, failure, message);
}

public sealed record CaseQueueCounts(
    int NotReady,
    int Review,
    int Held,
    int DueToday,
    int InToday,
    int SentToEngineer,
    int ReportsSent,
    DateTimeOffset AsOfUtc);

public sealed record AcceptCaseDraft(
    Guid ReceiptId,
    CaseType Type,
    bool InstructionsComplete,
    bool ImagesComplete,
    AssessmentFinding? AuditAssessment,
    Guid CorrelationId);

public interface ICaseAcceptance
{
    Task<CaseCommandResult> AcceptAsync(AcceptCaseDraft draft, StaffActor actor, CancellationToken cancellationToken);
}

public interface ICaseQueries
{
    Task<IReadOnlyList<CaseSummary>> ListAsync(CaseQuery query, StaffActor actor, CancellationToken cancellationToken);
    Task<CaseDetail?> GetAsync(Guid id, StaffActor actor, CancellationToken cancellationToken);
    Task<CaseQueueCounts> GetQueueCountsAsync(StaffActor actor, CancellationToken cancellationToken);
}

public interface ICaseWorkflow : ICaseQueries, IPermanentActionHistoryQueries
{
    Task<CaseCommandResult> ConfirmCompletenessAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, bool instructionsComplete, bool imagesComplete, CancellationToken cancellationToken);
    Task<CaseCommandResult> HoldAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
    Task<CaseCommandResult> ReleaseAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
    Task<CaseCommandResult> RecordChaseAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string channel, string target, string outcome, string? note, CancellationToken cancellationToken);
    Task<CaseCommandResult> StartReportPreparationAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, CancellationToken cancellationToken);
    Task<CaseCommandResult> RecordReportSentAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, CancellationToken cancellationToken);
    Task<CaseCommandResult> CloseAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, CaseTerminalOutcome outcome, string reason, CancellationToken cancellationToken);
    Task<CaseCommandResult> ReopenAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
    Task<CaseCommandResult> CreateCorrectPrincipalReplacementAsync(Guid id, string? leaseToken, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
}

public sealed record CaseLeaseResult(string? Token, CaseLeaseState State, CaseCommandFailure? Failure = null);

public interface ICaseEditing
{
    Task<CaseLeaseResult> AcquireAsync(Guid caseId, StaffActor actor, CancellationToken cancellationToken);
    Task<CaseLeaseResult> RenewAsync(Guid caseId, string token, StaffActor actor, CancellationToken cancellationToken);
    Task<CaseLeaseResult> ReleaseAsync(Guid caseId, string token, StaffActor actor, CancellationToken cancellationToken);
}
