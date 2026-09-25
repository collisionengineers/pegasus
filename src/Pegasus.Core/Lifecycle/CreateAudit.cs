using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// Create audit: an Inspection + Audit Case whose Inspection report is sent
/// gains its Audit work under the same Case. The Inspection's data is copied
/// into the Audit work, the Case moves straight to Report preparation with the
/// same Engineers, and the Audit report is referenced <c>a.{Case/PO}</c>. No
/// reason is asked: the action is its own record.
/// </summary>
/// <remarks>
/// <c>EditLeaseToken</c> is a non-nullable string, the lease-token type every
/// sibling lifecycle request (for example Return to Engineer) carries.
/// </remarks>
public sealed record CreateAuditRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken);

/// <summary>The validated command the store carries out.</summary>
public sealed record CreateAuditCommand(
    CreateAuditRequest Request,
    CaseIdentity Case,
    string AuditReference,
    Guid EngineerId);

public sealed record CreateAuditResult(
    CaseIdentity Case,
    Guid AuditWorkId,
    string AuditReference,
    bool IsReplay);

/// <summary>
/// What the store committed: the result and the custody work item that
/// creates the Audit's <c>a.</c> folder, published after the commit.
/// </summary>
public sealed record CreateAuditOutcome(CreateAuditResult Result, Guid CustodyWorkId);

public enum AuditRefusal
{
    NotInspectionAndAudit,
    CreatedInError,
    Archived,
    AuditAlreadyExists,
    ReportNotSent,
    NoAssignedEngineer
}

public sealed class AuditCreationException(Guid caseId, AuditRefusal refusal)
    : Exception(AuditPolicy.Message(refusal))
{
    public Guid CaseId { get; } = caseId;

    public AuditRefusal Refusal { get; } = refusal;
}

/// <summary>The one Create audit rule, shared by the use case and the Case page.</summary>
public static class AuditPolicy
{
    /// <summary>
    /// Why the Case cannot have its Audit created, or null when it can: an
    /// Inspection + Audit Case, not Created in error or archived, with no Audit
    /// work yet, whose Inspection report is sent (Post report, Completed or
    /// Query — never Held) and which has an assigned Engineer.
    /// </summary>
    public static AuditRefusal? Refusal(CaseType caseType, CaseWorkflowRecord workflow, CaseWorkSet? works)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        if (caseType != CaseType.InspectionAndAudit)
        {
            return AuditRefusal.NotInspectionAndAudit;
        }

        if (workflow.State == CaseLifecycleState.CreatedInError)
        {
            return AuditRefusal.CreatedInError;
        }

        if (workflow.Archive is not null)
        {
            return AuditRefusal.Archived;
        }

        if (works?.HasAudit == true)
        {
            return AuditRefusal.AuditAlreadyExists;
        }

        if (workflow.State is not (CaseLifecycleState.PostReport
                or CaseLifecycleState.PostReportComplete
                or CaseLifecycleState.Query)
            || workflow.ReportSentEvidence is null)
        {
            return AuditRefusal.ReportNotSent;
        }

        if (workflow.AssignedEngineerId is null)
        {
            return AuditRefusal.NoAssignedEngineer;
        }

        return null;
    }

    public static string Message(AuditRefusal refusal) => refusal switch
    {
        AuditRefusal.NotInspectionAndAudit => "Only an Inspection + Audit case can have an audit created from it.",
        AuditRefusal.CreatedInError => "A case recorded as created in error cannot have an audit created from it.",
        AuditRefusal.Archived => "An archived case cannot have an audit created from it.",
        AuditRefusal.AuditAlreadyExists => "This case already has its audit.",
        AuditRefusal.ReportNotSent => "Create audit is available once the report is sent.",
        AuditRefusal.NoAssignedEngineer => "Report preparation requires an assigned Engineer.",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null)
    };

    /// <summary>The history line: "Audit a.QDOS26214 created by {name}".</summary>
    public static string HistoryLine(string auditReference, string actorName) =>
        $"Audit {auditReference} created by {actorName}";
}

public interface ICreateAuditStore
{
    Task<CreateAuditOutcome> CreateAsync(CreateAuditCommand command, CancellationToken cancellationToken);
}

public interface ICreateAudit
{
    Task<CreateAuditResult> ExecuteAsync(CreateAuditRequest request, CancellationToken cancellationToken);
}

public sealed class CreateAudit(
    IGetCaseHeader cases,
    ICaseWorkflowQueries workflows,
    ICaseEngineerEligibility eligibility,
    ICreateAuditStore store,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher) : ICreateAudit
{
    private readonly IGetCaseHeader _cases = cases ?? throw new ArgumentNullException(nameof(cases));
    private readonly ICaseWorkflowQueries _workflows = workflows ?? throw new ArgumentNullException(nameof(workflows));
    private readonly ICaseEngineerEligibility _eligibility = eligibility
        ?? throw new ArgumentNullException(nameof(eligibility));
    private readonly ICreateAuditStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ICommittedExternalWorkPublisher _committedExternalWorkPublisher = committedExternalWorkPublisher
        ?? throw new ArgumentNullException(nameof(committedExternalWorkPublisher));

    public async Task<CreateAuditResult> ExecuteAsync(
        CreateAuditRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        // The same authorisation every Actions-menu lifecycle action applies.
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.CaseId == Guid.Empty || request.ExpectedVersion < 0)
        {
            throw new ArgumentException("A case identifier and expected version are required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OperationKey) || string.IsNullOrWhiteSpace(request.EditLeaseToken))
        {
            throw new ArgumentException("An operation key and an edit lease are required.", nameof(request));
        }

        var operationKey = request.OperationKey.Trim();
        var editLeaseToken = request.EditLeaseToken.Trim();
        if (operationKey.Length > 100 || editLeaseToken.Length > CaseEditAuthority.LeaseTokenLength)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The operation key or edit lease is too long.");
        }

        var header = await _cases.ExecuteAsync(new GetCaseHeaderQuery(request.CaseId, request.Actor), cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var workflow = header.Workflow;
        // A retried request is answered by the store from its committed record;
        // the Case has moved on since, so the rule is not asked again.
        var isReplay = await _workflows.HasOperationAsync(request.CaseId, operationKey, cancellationToken);
        if (!isReplay)
        {
            var refusal = AuditPolicy.Refusal(header.Summary.CaseType, workflow, header.Works);
            if (refusal is not null || workflow.AssignedEngineerId is not { } engineerId)
            {
                throw new AuditCreationException(request.CaseId, refusal ?? AuditRefusal.NoAssignedEngineer);
            }

            // Return to Engineer's eligibility check: the Case goes back to its Engineer.
            await CaseEngineerEligibilityPolicy.RequireEligibleAsync(_eligibility, engineerId, cancellationToken);
        }

        var identity = workflow.Identity;
        var outcome = await _store.CreateAsync(
            new CreateAuditCommand(
                request with { OperationKey = operationKey, EditLeaseToken = editLeaseToken },
                identity,
                CaseReferenceFormat.AuditReport(identity.Reference),
                workflow.AssignedEngineerId ?? Guid.Empty),
            cancellationToken);
        if (!outcome.Result.IsReplay)
        {
            await _committedExternalWorkPublisher.PublishAsync(outcome.CustodyWorkId, cancellationToken);
        }

        return outcome.Result;
    }
}
