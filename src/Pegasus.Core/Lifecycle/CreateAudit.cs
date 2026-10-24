using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// Create audit: an Inspection + Audit Case in work gains its Audit work
/// under the same Case, whether or not its Inspection report has been sent
/// (operator, 1 October 2026). The Inspection's data is copied into the Audit
/// work, the Case moves straight to Report preparation with the same
/// Engineers, and the Audit report is referenced <c>a.{Case/PO}</c>. No
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
    Held,
    Closed,
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
    /// work yet, in work (never Held, never under a closed disposition) and
    /// with an assigned Engineer. Whether the Inspection report has been sent
    /// does not matter (operator, 1 October 2026).
    /// </summary>
    public static AuditRefusal? Refusal(CaseType caseType, CaseWorkflowRecord workflow, CaseWorkSet? works)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return Refusal(
            caseType,
            workflow.State,
            workflow.Archive is not null,
            works?.HasAudit == true,
            workflow.AssignedEngineerId is not null);
    }

    /// <summary>
    /// The same rule asked of the facts the store reads under its lock, so the
    /// refusal and its order are one.
    /// </summary>
    public static AuditRefusal? Refusal(
        CaseType caseType,
        CaseLifecycleState state,
        bool archived,
        bool hasAudit,
        bool hasAssignedEngineer)
    {
        if (caseType != CaseType.InspectionAndAudit)
        {
            return AuditRefusal.NotInspectionAndAudit;
        }

        if (state == CaseLifecycleState.CreatedInError)
        {
            return AuditRefusal.CreatedInError;
        }

        if (archived)
        {
            return AuditRefusal.Archived;
        }

        if (hasAudit)
        {
            return AuditRefusal.AuditAlreadyExists;
        }

        if (state == CaseLifecycleState.Held)
        {
            return AuditRefusal.Held;
        }

        if (CaseLifecycleRules.IsTerminal(state))
        {
            return AuditRefusal.Closed;
        }

        if (!hasAssignedEngineer)
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
        AuditRefusal.Held => "A held case cannot have an audit created from it.",
        AuditRefusal.Closed => "A closed case cannot have an audit created from it.",
        AuditRefusal.NoAssignedEngineer => CaseLifecycleRules.AssignedEngineerRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, null)
    };

    /// <summary>The history line: "Audit a.QDOS26214 created by {name}".</summary>
    public static string HistoryLine(string auditReference, string actorName) =>
        $"Audit {auditReference} created by {actorName}";
}

/// <summary>
/// Carries out Create audit in one transaction. The store reads the Case under
/// its lock and applies <see cref="AuditPolicy"/> and the assigned Engineer's
/// eligibility to what it read, except for a replay of an operation it already
/// committed, which it answers from its record.
/// </summary>
public interface ICreateAuditStore
{
    Task<CreateAuditOutcome> CreateAsync(CreateAuditRequest request, CancellationToken cancellationToken);
}

public interface ICreateAudit
{
    Task<CreateAuditResult> ExecuteAsync(CreateAuditRequest request, CancellationToken cancellationToken);
}

public sealed class CreateAudit(
    ICreateAuditStore store,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher) : ICreateAudit
{
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

        var outcome = await _store.CreateAsync(
            request with { OperationKey = operationKey, EditLeaseToken = editLeaseToken },
            cancellationToken);
        if (!outcome.Result.IsReplay)
        {
            await _committedExternalWorkPublisher.PublishAsync(outcome.CustodyWorkId, cancellationToken);
        }

        return outcome.Result;
    }
}
