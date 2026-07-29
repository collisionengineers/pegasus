namespace Pegasus.Core.Cases;

public enum OrganizationRole
{
    WorkProvider,
    InstructionIntermediary
}

public sealed record Organization(
    Guid Id,
    string Name,
    IReadOnlyList<OrganizationRole> Roles,
    long Version);

public sealed record Principal(
    Guid Id,
    Guid OrganizationId,
    string Code,
    Guid SequenceLineageId,
    Guid? PredecessorId,
    Guid? SuccessorId,
    bool IsActive,
    long Version);

public enum CaseType
{
    Inspection,
    Audit,
    InspectionAndAudit
}

public enum AuditAssessment
{
    Repairable,
    TotalLoss
}

public enum CaseInitialState
{
    NotReady,
    Review
}

public enum CaseCustodyState
{
    Pending,
    Confirmed,
    Failed
}

public sealed record CaseCompleteness(
    bool InstructionComplete,
    bool ImagesComplete,
    bool InstructionConfirmedByStaff,
    bool ImagesConfirmedByStaff)
{
    public bool IsReadyForReview(bool automaticallyDefinitive) =>
        InstructionComplete
        && ImagesComplete
        && (automaticallyDefinitive || (InstructionConfirmedByStaff && ImagesConfirmedByStaff));
}

public sealed record CaseIdentity(
    Guid CaseId,
    string PrincipalCode,
    int Year,
    int Sequence,
    string Reference,
    string? AuditReference = null);

public sealed record CaseAcceptanceRequest(
    Guid IntakeReceiptId,
    long ExpectedIntakeVersion,
    string Actor,
    string OperationKey,
    CaseType CaseType,
    string PrincipalCode,
    CaseCompleteness Completeness,
    AuditAssessment? StandaloneAuditAssessment);

public sealed record CaseAcceptanceOutcome(
    CaseIdentity Identity,
    CaseInitialState InitialState,
    CaseCustodyState CustodyState,
    Guid CustodyWorkId,
    bool IsDuplicate);

public sealed class CaseIdentitySequenceExhaustedException(string principalCode, int year)
    : Exception($"The principal '{principalCode}' has exhausted its {year} case identity sequence.")
{
    public string PrincipalCode { get; } = principalCode;

    public int Year { get; } = year;
}

public sealed class CaseAcceptanceOperationConflictException(
    Guid intakeReceiptId,
    string operationKey)
    : InvalidOperationException(
        $"Intake receipt '{intakeReceiptId}' was already accepted by a different operation or with different inputs.")
{
    public Guid IntakeReceiptId { get; } = intakeReceiptId;

    public string OperationKey { get; } = operationKey;
}

/// <summary>
/// Persists the acceptance transaction. Implementations allocate the principal-lineage/year
/// sequence, create the case and intake link, record history, and enqueue custody as one commit.
/// </summary>
public interface ICaseAcceptanceStore
{
    Task<CaseAcceptanceOutcome> AcceptAsync(
        CaseAcceptanceRequest request,
        CancellationToken cancellationToken);
}

public sealed record CreateLinkedReplacementRequest(
    Guid CaseId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    string ReplacementPrincipalCode);

public interface ICreateLinkedReplacement
{
    Task<CaseAcceptanceOutcome> ExecuteAsync(
        CreateLinkedReplacementRequest request,
        CancellationToken cancellationToken);
}

public sealed record CreateOrganizationRequest(
    string Name,
    IReadOnlyList<OrganizationRole> Roles,
    string Actor,
    string OperationKey);

public sealed record UpdateOrganizationRolesRequest(
    Guid OrganizationId,
    long ExpectedVersion,
    IReadOnlyList<OrganizationRole> Roles,
    string Actor,
    string OperationKey,
    string Reason);

public sealed record CreatePrincipalRequest(
    Guid OrganizationId,
    string Code,
    string Actor,
    string OperationKey);

public sealed record ReplacePrincipalRequest(
    Guid PrincipalId,
    long ExpectedVersion,
    Guid SuccessorOrganizationId,
    string SuccessorCode,
    string Actor,
    string OperationKey,
    string Reason);

public sealed record RecordEngineerFindingRequest(
    Guid CaseId,
    long ExpectedVersion,
    string Actor,
    string OperationKey,
    string Reason,
    AuditAssessment Assessment);

public interface ICreateOrganization
{
    Task<Organization> ExecuteAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);
}

public interface IUpdateOrganizationRoles
{
    Task<Organization> ExecuteAsync(
        UpdateOrganizationRolesRequest request,
        CancellationToken cancellationToken);
}

public interface ICreatePrincipal
{
    Task<Principal> ExecuteAsync(CreatePrincipalRequest request, CancellationToken cancellationToken);
}

public interface IReplacePrincipal
{
    Task<Principal> ExecuteAsync(ReplacePrincipalRequest request, CancellationToken cancellationToken);
}

public interface IRecordEngineerFinding
{
    Task<CaseIdentity> ExecuteAsync(
        RecordEngineerFindingRequest request,
        CancellationToken cancellationToken);
}
