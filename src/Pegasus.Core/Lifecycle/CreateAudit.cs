using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// Create audit: an Inspection + Audit Case in work gains its Audit work
/// under the same Case, whether or not its Inspection report has been sent
/// (operator, 1 October 2026). The Inspection's data is copied into the Audit
/// work, with the decisions <see cref="AuditPolicy.ResetsFor"/> names left
/// open for the Audit to make again (R729); the Case moves straight to Report
/// preparation with the same Engineers, and the Audit report is referenced
/// <c>a.{Case/PO}</c>. No reason is asked: the action is its own record.
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

/// <summary>
/// The Inspection decisions the Audit's copy starts without, so that the
/// Audit's report waits for them to be made again (R729).
/// </summary>
/// <param name="OpenValuation">
/// No valuation is Selected: the guide cards keep their values, the applied
/// valuations are not copied and Retail, Trade and Engineer's Value start empty.
/// </param>
/// <param name="ClearSalvageValue">The Salvage value starts empty; the category is kept.</param>
/// <param name="OpenRepairSpec">Every repair spec is copied and none is Current.</param>
public sealed record AuditStartingResets(bool OpenValuation, bool ClearSalvageValue, bool OpenRepairSpec)
{
    public static readonly AuditStartingResets None = new(false, false, false);
}

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

    /// <summary>
    /// What the Audit's copy leaves open, from the Inspection's outcome when
    /// Create audit runs (operator, 6 November 2026; R729). A Total loss opens
    /// the valuation and the Salvage value; Repairable, Cash in lieu and
    /// Contract repair open the repair spec; no outcome copies everything.
    /// </summary>
    public static AuditStartingResets ResetsFor(string? inspectionOutcome) => inspectionOutcome switch
    {
        "total_loss" => new AuditStartingResets(OpenValuation: true, ClearSalvageValue: true, OpenRepairSpec: false),
        "repairable" or "cash_in_lieu" or "contract_repair" =>
            new AuditStartingResets(OpenValuation: false, ClearSalvageValue: false, OpenRepairSpec: true),
        _ => AuditStartingResets.None
    };

    /// <summary>The assessment field paths the Audit's copy starts without.</summary>
    public static IReadOnlySet<string> OpenedFieldPaths(AuditStartingResets resets)
    {
        ArgumentNullException.ThrowIfNull(resets);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        if (resets.OpenValuation)
        {
            paths.Add(AssessmentVocabulary.ValueRetail);
            paths.Add(AssessmentVocabulary.ValueTrade);
            paths.Add(AssessmentVocabulary.ValueEngineer);
        }

        if (resets.ClearSalvageValue)
        {
            paths.Add(AssessmentVocabulary.SalvageValue);
        }

        return paths;
    }

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
