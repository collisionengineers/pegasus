using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Lifecycle;

/// <summary>
/// The one owner of the garage authorisation milestone on a Garage
/// authorisation and invoice Audit (operator, 17 November 2026; R1142 to
/// R1144, R1159, R1161). The Engineer authorises the garage outside Pegasus
/// and marks it done here: the Case then waits in Not ready, keeping its
/// Engineer, for the repairer's invoice, and returns to With Engineer when a
/// confirmed Repair invoice is filed. A total loss or cash in lieu Outcome
/// waives both the authorisation and the invoice. The page, its handler, the
/// Automation tool and the store all ask these rules.
/// </summary>
public static class GarageAuthorisationPolicy
{
    /// <summary>The history reason Mark done records; the act takes none from staff (R1161).</summary>
    public const string HistoryReason = "Garage authorised.";

    /// <summary>The chase reason while the Case waits for the repairer's invoice (R1144).</summary>
    public const string AwaitingRepairInvoice = "Awaiting repair invoice";

    // The two refusals and the three history reasons below are worded as
    // the operator kept them (18 November 2026; R1165).
    public const string AuthoriseNotOffered = "Authorise garage is not offered on this Case.";
    public const string UndoNotOffered = "Undo garage authorisation is not offered on this Case.";

    public const string AuthorisedEventType = "garage_authorised";
    public const string UndoneEventType = "garage_authorisation_undone";

    /// <summary>The event the reconciliation records when it returns the Case to With Engineer.</summary>
    public const string ReturnedEventType = "garage_wait_ended";

    /// <summary>The event it records when the garage's estimate completes the Case's requirements.</summary>
    public const string RequirementsCompletedEventType = "garage_estimate_completed_requirements";

    // The history reasons of those two events: what was filed or recorded.
    public const string RepairInvoiceFiledReason = "A confirmed Repair invoice was filed.";
    public const string OutcomeWaivedReason = "The recorded Outcome needs no repair invoice.";
    public const string ExternalEstimateFiledReason = "A confirmed External estimate was filed.";

    /// <summary>Whether the Case is a standalone Audit on the Garage authorisation and invoice workflow.</summary>
    public static bool Applies(CaseType caseType, AuditWorkflowKind auditWorkflow) =>
        caseType == CaseType.Audit && auditWorkflow == AuditWorkflowKind.GarageAuthorisationAndInvoice;

    /// <summary>
    /// Whether the recorded Outcome waives the authorisation and the repair
    /// invoice: a total loss or a cash in lieu has no repair to authorise or
    /// to invoice (R1143).
    /// </summary>
    public static bool IsWaived(string? outcomeCode) =>
        outcomeCode is "total_loss" or "cash_in_lieu";

    /// <summary>
    /// Authorise garage is offered while such an Audit is With Engineer with
    /// an Engineer, its garage is not yet authorised and its Outcome does not
    /// waive it (R1142, R1143).
    /// </summary>
    public static bool CanAuthorise(
        CaseLifecycleState state,
        Guid? assignedEngineerId,
        CaseGarageFacts garage) =>
        garage is { Applies: true, Authorised: false, Waived: false }
        && state == CaseLifecycleState.ReportPreparation
        && assignedEngineerId is not null;

    public static void RequireCanAuthorise(
        CaseLifecycleState state,
        Guid? assignedEngineerId,
        CaseGarageFacts garage)
    {
        if (!CanAuthorise(state, assignedEngineerId, garage))
        {
            throw new InvalidOperationException(AuthoriseNotOffered);
        }
    }

    /// <summary>
    /// Undo garage authorisation is offered on such an Audit once its garage
    /// is authorised, while the Case is open and not held (R1142).
    /// </summary>
    public static bool CanUndo(CaseLifecycleState state, bool isArchived, CaseGarageFacts garage) =>
        garage is { Applies: true, Authorised: true }
        && !isArchived
        && state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation
            or CaseLifecycleState.ReportSent;

    public static void RequireCanUndo(CaseLifecycleState state, bool isArchived, CaseGarageFacts garage)
    {
        if (!CanUndo(state, isArchived, garage))
        {
            throw new InvalidOperationException(UndoNotOffered);
        }
    }

    /// <summary>
    /// Whether marking the garage authorised leaves the Case waiting in Not
    /// ready: it does unless a confirmed Repair invoice is already filed, in
    /// which case there is nothing to wait for and the Case stays With
    /// Engineer (R1144).
    /// </summary>
    public static bool WaitsAfterAuthorisation(CaseGarageFacts garage) => !garage.RepairInvoiceFiled;

    /// <summary>
    /// Whether a Case waiting for its repair invoice returns to With Engineer
    /// now: a confirmed Repair invoice is filed (R1144), the Outcome waives it
    /// (R1143), or the authorisation was undone (R1159).
    /// </summary>
    public static bool WaitIsOver(CaseLifecycleState state, CaseGarageFacts garage) =>
        state == CaseLifecycleState.NotReady
        && garage is { Applies: true, Authorised: true }
        && (garage.RepairInvoiceFiled || garage.Waived);
}

/// <summary>
/// Moves on the Garage authorisation and invoice Audits whose wait a stored
/// fact has ended: a confirmed Repair invoice or a waiving Outcome returns a
/// waiting Case to With Engineer (R1143, R1144), and a confirmed External
/// estimate lets a complete Case into Review (R1141). The staff acts that
/// file the fact move the Case in their own save (R1195); this sweep moves it
/// for every other route a file or an Outcome arrives by.
/// </summary>
public interface IGarageWaitStore
{
    /// <summary>Reconciles up to <paramref name="limit"/> Cases and returns how many moved.</summary>
    Task<int> ReconcileAsync(int limit, CancellationToken cancellationToken);
}

public sealed class ReconcileGarageWaits(IGarageWaitStore store)
{
    private readonly IGarageWaitStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Task<int> ExecuteAsync(int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        return _store.ReconcileAsync(limit, cancellationToken);
    }
}

/// <summary>Marks the garage authorised (R1142).</summary>
public sealed class AuthoriseGarage(IGarageAuthorisationStore store)
{
    private readonly IGarageAuthorisationStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        AuthoriseGarageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // The history reason is Core's for every caller (R1161), composed
        // before the replay check: the operation's hash covers it.
        request = request with { Reason = GarageAuthorisationPolicy.HistoryReason };
        CaseLifecycleRules.ValidateMutation(request);
        return await _store.AuthoriseGarageAsync(request, cancellationToken);
    }
}

/// <summary>Takes the garage's authorisation back, with a reason (R1142).</summary>
public sealed class UndoGarageAuthorisation(IGarageAuthorisationStore store)
{
    private readonly IGarageAuthorisationStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseWorkflowRecord> ExecuteAsync(
        UndoGarageAuthorisationRequest request,
        CancellationToken cancellationToken)
    {
        CaseLifecycleRules.ValidateMutation(request);
        return await _store.UndoGarageAuthorisationAsync(request, cancellationToken);
    }
}
