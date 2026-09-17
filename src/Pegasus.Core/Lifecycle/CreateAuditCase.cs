using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// Create audit (decided 13 September): an Inspection + Audit Case, once a
/// report has been generated on it, gets a separate linked Audit Case — a
/// duplicate of the original with the same Principal, parties, vehicle,
/// incident, inspection details, figures, files and estimate, of type Audit,
/// starting in Review with the original's engineer. Its reference is
/// <c>a.{Case/PO}</c> for every recorded outcome. The assessment remains on the
/// Audit Case as a separate fact. No reason is asked: the action is its own
/// record.
/// </summary>
public sealed record CreateAuditCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken);

/// <summary>The validated command the store carries out: the source, the derived identity and the assessment behind it.</summary>
public sealed record CreateAuditCaseCommand(
    CreateAuditCaseRequest Request,
    CaseIdentity Source,
    AuditAssessment Assessment,
    string AuditReference,
    Guid? EngineerId);

public sealed record CreateAuditCaseResult(
    CaseIdentity Source,
    CaseIdentity AuditCase,
    AuditAssessment Assessment,
    bool IsReplay);

public enum AuditCaseRefusal
{
    NotInspectionAndAudit,
    AuditCaseAlreadyExists,
    SourceCreatedInError,
    SourceArchived,
    NoGeneratedReport,
    NoRecordedOutcome,
    NotStaff
}

public sealed class AuditCaseCreationException(AuditCaseRefusal refusal)
    : InvalidOperationException(Describe(refusal))
{
    public AuditCaseRefusal Refusal { get; } = refusal;

    private static string Describe(AuditCaseRefusal refusal) => refusal switch
    {
        AuditCaseRefusal.NotInspectionAndAudit => "Only an Inspection + Audit case can have an audit created from it.",
        AuditCaseRefusal.AuditCaseAlreadyExists => "This case already has its audit case.",
        AuditCaseRefusal.SourceCreatedInError => "A case recorded as created in error cannot have an audit created from it.",
        AuditCaseRefusal.SourceArchived => "An archived case cannot have an audit created from it.",
        AuditCaseRefusal.NoGeneratedReport => "Create audit is available once a report has been generated on the case.",
        AuditCaseRefusal.NoRecordedOutcome => "The case has no recorded assessment outcome to derive the audit reference from.",
        AuditCaseRefusal.NotStaff => "Only a member of staff can create an audit case.",
        _ => "The audit case cannot be created."
    };
}

/// <summary>The two-way link between an Inspection + Audit Case and its Audit Case, for the record bars and lists.</summary>
public sealed record CaseAuditLink(Guid CaseId, string Reference);

public interface ICaseAuditLinkQueries
{
    /// <summary>The Audit Case created from this Case, if any.</summary>
    Task<CaseAuditLink?> GetAuditCaseAsync(Guid sourceCaseId, CancellationToken cancellationToken);

    /// <summary>The original Case this Audit Case was created from, if it is one.</summary>
    Task<CaseAuditLink?> GetOriginalCaseAsync(Guid auditCaseId, CancellationToken cancellationToken);
}

/// <summary>Whether a report has ever been generated on the Case: any generation with a confirmed artifact.</summary>
public interface ICaseReportGeneratedQueries
{
    Task<bool> HasGeneratedReportAsync(Guid caseId, CancellationToken cancellationToken);
}

public interface ICreateAuditCaseStore
{
    Task<CreateAuditCaseResult> CreateAsync(CreateAuditCaseCommand command, CancellationToken cancellationToken);
}

public interface ICreateAuditCase
{
    Task<CreateAuditCaseResult> ExecuteAsync(CreateAuditCaseRequest request, CancellationToken cancellationToken);
}

public static class AuditCasePolicy
{
    /// <summary>
    /// The assessment represented by the recorded <c>assessment.outcome</c>.
    /// Repairable, cash in lieu and contract repair are repairable-basis
    /// outcomes. Anything else is no outcome.
    /// </summary>
    public static AuditAssessment? AssessmentFor(string? outcome) => outcome?.Trim().ToLowerInvariant() switch
    {
        "total_loss" => AuditAssessment.TotalLoss,
        "repairable" or "cash_in_lieu" or "contract_repair" => AuditAssessment.Repairable,
        _ => null
    };

    /// <summary>The history line on the original: "Audit case a.QDOS26214 created by {name} from QDOS26214 — total loss".</summary>
    public static string SourceHistoryLine(string auditReference, string actorName, string sourceReference, AuditAssessment assessment) =>
        $"Audit case {auditReference} created by {actorName} from {sourceReference} — {Describe(assessment)}";

    public static string AuditHistoryLine(string auditReference, string actorName, string sourceReference, AuditAssessment assessment) =>
        $"Audit case {auditReference} created by {actorName} from {sourceReference} — {Describe(assessment)}";

    public static string Describe(AuditAssessment assessment) => assessment switch
    {
        AuditAssessment.TotalLoss => "total loss",
        AuditAssessment.Repairable => "repairable",
        _ => throw new ArgumentOutOfRangeException(nameof(assessment), assessment, null)
    };
}

public sealed class CreateAuditCase(
    IGetCaseHeader cases,
    ICaseAssessmentStore assessment,
    ICaseReportGeneratedQueries reports,
    ICaseAuditLinkQueries links,
    ICreateAuditCaseStore store) : ICreateAuditCase
{
    private readonly IGetCaseHeader _cases = cases ?? throw new ArgumentNullException(nameof(cases));
    private readonly ICaseAssessmentStore _assessment = assessment ?? throw new ArgumentNullException(nameof(assessment));
    private readonly ICaseReportGeneratedQueries _reports = reports ?? throw new ArgumentNullException(nameof(reports));
    private readonly ICaseAuditLinkQueries _links = links ?? throw new ArgumentNullException(nameof(links));
    private readonly ICreateAuditCaseStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CreateAuditCaseResult> ExecuteAsync(
        CreateAuditCaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        if (request.CaseId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException("A case identifier and expected version are required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OperationKey) || string.IsNullOrWhiteSpace(request.EditLeaseToken))
        {
            throw new ArgumentException("An operation key and an edit lease are required.", nameof(request));
        }

        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Actor.Kind != ActorKind.Staff)
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.NotStaff);
        }

        var header = await _cases.ExecuteAsync(new GetCaseHeaderQuery(request.CaseId, request.Actor), cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        if (header.Summary.CaseType != CaseType.InspectionAndAudit)
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.NotInspectionAndAudit);
        }

        if (header.Workflow.State == CaseLifecycleState.CreatedInError)
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.SourceCreatedInError);
        }

        if (header.Workflow.Archive is not null)
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.SourceArchived);
        }

        if (await _links.GetAuditCaseAsync(request.CaseId, cancellationToken) is not null)
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.AuditCaseAlreadyExists);
        }

        if (!await _reports.HasGeneratedReportAsync(request.CaseId, cancellationToken))
        {
            throw new AuditCaseCreationException(AuditCaseRefusal.NoGeneratedReport);
        }

        var projection = await _assessment.GetAsync(request.CaseId, cancellationToken);
        var assessment = AuditCasePolicy.AssessmentFor(projection?.Field(AssessmentVocabulary.Outcome)?.Value)
            ?? throw new AuditCaseCreationException(AuditCaseRefusal.NoRecordedOutcome);

        var identity = header.Workflow.Identity;
        return await _store.CreateAsync(
            new CreateAuditCaseCommand(
                request with { OperationKey = request.OperationKey.Trim() },
                identity,
                assessment,
                AuditIdentity.Create(identity.Reference),
                header.Workflow.AssignedEngineerId),
            cancellationToken);
    }
}
