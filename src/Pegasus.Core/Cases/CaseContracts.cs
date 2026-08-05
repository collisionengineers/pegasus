using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

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
    long Version,
    CaseInspectionMode InspectionMode = CaseInspectionMode.PhysicalAddress);

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
/// <summary>
/// The QDOS principal, as seeded. A code, not a gate.
/// </summary>
/// <remarks>
/// This is here for the seeds and the tests that need to name the principal
/// the alpha runs on. It authorises nothing: which principals may hold a case
/// is a question about which principals exist and are active, answered by the
/// principal record inside the acceptance transaction.
/// </remarks>
public static class QdosPrincipal
{
    public const string Code = "QDOS";
}

/// <summary>
/// The shape of a principal code on its way into an allocation.
/// </summary>
/// <remarks>
/// This replaced <c>QdosAlphaCaseActivationPolicy.RequireActivatedPrincipal</c>,
/// which refused every principal but <c>QDOS</c>. Nothing about the business
/// asked for that: allocation fails closed on a principal that does not exist
/// or is not active, and the acceptance transaction already establishes both
/// against the principal record. The activation check was a second, blunter
/// rule sitting in front of the real one, and it made a correctly registered
/// principal unusable.
///
/// What remains is the shape a code must have to be looked up at all. Reading
/// a non-QDOS principal *out of a document* is a separate matter and is still
/// not implemented — the extraction policy recognises QDOS only, so another
/// principal reaches allocation because a person keyed it, not because
/// anything inferred it.
/// </remarks>
public static class CasePrincipalCode
{
    public const int MaximumLength = 20;

    public static string Normalize(string principalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);
        var normalized = principalCode.Trim().ToUpperInvariant();
        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"The principal code must be {MaximumLength} characters or fewer.",
                nameof(principalCode));
        }

        return normalized;
    }
}

public static class AuditIdentity
{
    public static string Create(string caseReference, AuditAssessment assessment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
        var prefix = assessment switch
        {
            AuditAssessment.Repairable => "a.",
            AuditAssessment.TotalLoss => "ap.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(assessment),
                "The Audit assessment is invalid.")
        };
        return prefix + caseReference;
    }
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

public sealed record StandaloneAuditEvidence(
    Guid Id,
    Guid IntakeReceiptId,
    Guid OriginalReportAssetId,
    AuditAssessment Assessment,
    Guid ConfirmedByStaffId,
    DateTimeOffset ConfirmedAtUtc,
    string Reason,
    long ReceiptVersion,
    bool IsDuplicate);

public sealed record ConfirmStandaloneAuditEvidenceRequest(
    Guid EvidenceId,
    Guid IntakeReceiptId,
    long ExpectedIntakeVersion,
    Guid OriginalReportAssetId,
    AuditAssessment Assessment,
    ActionActor Actor,
    string OperationKey,
    string Reason);
public static class StandaloneAuditEvidencePolicy
{
    public static Guid ValidateConfirmation(ConfirmStandaloneAuditEvidenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EvidenceId == Guid.Empty
            || request.IntakeReceiptId == Guid.Empty
            || request.OriginalReportAssetId == Guid.Empty)
        {
            throw new ArgumentException(
                "Standalone Audit evidence requires stable receipt, report, and evidence identities.",
                nameof(request));
        }
        if (request.ExpectedIntakeVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected intake version cannot be negative.");
        }
        if (!Enum.IsDefined(request.Assessment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The original-report assessment is invalid.");
        }
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Actor.Kind != ActorKind.Staff
            || !Guid.TryParse(request.Actor.SubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Standalone Audit evidence must be confirmed by an authenticated staff member.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        if (request.OperationKey.Length > 100 || request.Reason.Length > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The evidence operation key or reason exceeds its supported length.");
        }

        return staffId;
    }
}


public sealed class StandaloneAuditEvidenceConflictException(Guid intakeReceiptId)
    : InvalidOperationException(
        $"Standalone Audit evidence for intake receipt '{intakeReceiptId}' was already confirmed with different evidence.")
{
    public Guid IntakeReceiptId { get; } = intakeReceiptId;
}

public interface IConfirmStandaloneAuditEvidence
{
    Task<StandaloneAuditEvidence> ExecuteAsync(
        ConfirmStandaloneAuditEvidenceRequest request,
        CancellationToken cancellationToken);
}

public interface IStandaloneAuditEvidenceQueries
{
    Task<StandaloneAuditEvidence?> GetForReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);
}

public sealed record CaseAcceptanceRequest(
    Guid IntakeReceiptId,
    long ExpectedIntakeVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    CaseType CaseType,
    string PrincipalCode,
    CaseCompleteness Completeness,
    CaseCompletenessEvaluation CompletenessEvaluation,
    CaseInspectionMode ProviderInspectionMode,
    Guid? StandaloneAuditEvidenceId = null,
    DateOnly? AcceptedInspectionDeadline = null);

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
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    string ReplacementPrincipalCode)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ICreateLinkedReplacement
{
    Task<CaseAcceptanceOutcome> ExecuteAsync(
        CreateLinkedReplacementRequest request,
        CancellationToken cancellationToken);
}
public interface ILinkedCaseReplacementStore
{
    Task<CaseAcceptanceOutcome> CreateAsync(
        CreateLinkedReplacementRequest request,
        CancellationToken cancellationToken);
}


public sealed record CreateOrganizationRequest(
    string Name,
    IReadOnlyList<OrganizationRole> Roles,
    ActionActor Actor,
    string OperationKey);

public sealed record UpdateOrganizationRolesRequest(
    Guid OrganizationId,
    long ExpectedVersion,
    IReadOnlyList<OrganizationRole> Roles,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record CreatePrincipalRequest(
    Guid OrganizationId,
    string Code,
    ActionActor Actor,
    string OperationKey,
    CaseInspectionMode InspectionMode = CaseInspectionMode.PhysicalAddress);

public sealed record ReplacePrincipalRequest(
    Guid PrincipalId,
    long ExpectedVersion,
    Guid SuccessorOrganizationId,
    string SuccessorCode,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record RecordEngineerFindingRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    AuditAssessment Assessment)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public static class EngineerFindingPolicy
{
    public static Guid ValidateRequest(RecordEngineerFindingRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        if (!Enum.IsDefined(request.Assessment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The Engineer finding assessment is invalid.");
        }
        if (request.Actor.Kind != ActorKind.Staff
            || !request.Actor.IsInRole(StaffRole.Engineer)
            || !Guid.TryParse(request.Actor.SubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "An Engineer finding must be recorded by an authenticated Engineer.");
        }

        return staffId;
    }

    public static void RequireAssignedInspectionAndAudit(
        CaseType caseType,
        CaseLifecycleState state,
        Guid? assignedEngineerId,
        Guid actingEngineerId)
    {
        if (caseType != CaseType.InspectionAndAudit)
        {
            throw new InvalidOperationException(
                "An Engineer finding can allocate a later Audit identity only for an Inspection and Audit case.");
        }
        if (state != CaseLifecycleState.ReportPreparation)
        {
            throw new InvalidOperationException(
                "An Engineer finding can be recorded only during Report preparation.");
        }
        if (assignedEngineerId != actingEngineerId)
        {
            throw new InvalidOperationException(
                "Only the Engineer assigned to this case can record the finding.");
        }
    }
}

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
