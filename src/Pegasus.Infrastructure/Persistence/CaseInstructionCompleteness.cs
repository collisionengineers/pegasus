using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Case's instruction completeness, read from its own accepted data. A
/// Case created directly is complete only when every required instruction item
/// is answered, so the stored fact is re-checked wherever those items are
/// written: when the last one lands the blocker clears on its own (FRD-13).
/// The check only ever clears it; clearing a field afterwards does not block
/// the Case again (operator, 2 November 2026).
/// </summary>
internal static class CaseInstructionCompleteness
{
    /// <summary>The required instruction items the snapshot's accepted data does not answer.</summary>
    internal static IReadOnlyList<string> Missing(CaseDataSnapshotEntity snapshot) =>
        InstructionDraftCompleteness.MissingCaseFieldNames(
            CaseDataFieldWriter.ReadEditable(snapshot),
            snapshot.Work.Case.Principal.ReferenceScheme == PrincipalReferenceSchemes.Registration);

    /// <summary>The Case's own mileage, as its accepted data answers it.</summary>
    internal static long? Mileage(CaseDataSnapshotEntity snapshot) =>
        CaseDataFieldWriter.ReadEditable(snapshot).VehicleMileage;

    /// <summary>
    /// Whether the mileage gate lets the Case into Review from its stored
    /// state (R759): a Case with no mileage stays Not ready; one past Not
    /// ready has passed the gate.
    /// </summary>
    internal static bool MileageGateOpen(CaseDataSnapshotEntity snapshot, string workflowState) =>
        CaseCompletenessPolicy.MileageGateOpen(Enum.Parse<CaseLifecycleState>(workflowState), Mileage(snapshot));

    /// <summary>The mileage gate (<see cref="MileageGateOpen(CaseDataSnapshotEntity, string)"/>) read from the Case's stored data.</summary>
    internal static async Task<bool> MileageGateOpenAsync(
        PegasusDbContext db, Guid caseId, string workflowState, CancellationToken cancellationToken)
    {
        var snapshot = await EfCaseDataStore.SnapshotQuery(db, tracking: false)
            .SingleOrDefaultAsync(item => item.WorkId == caseId, cancellationToken)
            ?? throw new InvalidDataException("The Case has no data snapshot.");
        return MileageGateOpen(snapshot, workflowState);
    }

    /// <summary>
    /// Marks the Case's instruction complete once its data answers every
    /// required item. Returns whether the stored fact changed.
    /// </summary>
    internal static bool CompleteIfPresent(CaseDataSnapshotEntity snapshot)
    {
        var caseEntity = snapshot.Work.Case;
        if (caseEntity.InstructionComplete || Missing(snapshot).Count > 0)
        {
            return false;
        }

        caseEntity.InstructionComplete = true;
        return true;
    }

    /// <summary>
    /// Re-evaluates readiness from the Case's stored facts after a system
    /// write changed one, and records the policy it used: a Not ready or
    /// Review Case with no Engineer moves to Review when nothing required is
    /// missing, the mileage included (R759), and otherwise stays Not ready
    /// with its chase scheduled.
    /// </summary>
    internal static async Task<CaseCompletenessEvaluation> ApplyReadinessAsync(
        PegasusDbContext db,
        CaseWorkflowEntity workflow,
        CaseDataSnapshotEntity snapshot,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        // The chase to stop or reschedule is the Case's stored one.
        await db.Entry(workflow).Reference(item => item.DueWork).LoadAsync(cancellationToken);
        var configuration = await EfWorkflowConfigurationStore.ReadAsync(db, cancellationToken);
        var evaluation = CaseCompletenessPolicy.Evaluate(
            new CaseCompleteness(
                snapshot.Work.Case.InstructionComplete,
                snapshot.Work.Case.ImagesComplete),
            configuration,
            MileageGateOpen(snapshot, workflow.State));
        snapshot.CompletenessPolicyKey = evaluation.PolicyKey;
        snapshot.CompletenessPolicyVersion = evaluation.PolicyVersion;
        snapshot.CompletenessPolicySatisfied = evaluation.SatisfiesPolicy;
        var mayRecalculateWorkflow = workflow.AssignedEngineerId is null
            && workflow.State is nameof(CaseLifecycleState.NotReady)
                or nameof(CaseLifecycleState.Review);
        if (mayRecalculateWorkflow && evaluation.SatisfiesPolicy)
        {
            var enteringReview = workflow.State != nameof(CaseLifecycleState.Review);
            workflow.State = nameof(CaseLifecycleState.Review);
            CaseChaseState.Stop(workflow);
            if (enteringReview)
            {
                workflow.StateEnteredAtUtc = nowUtc;
            }
        }
        else if (mayRecalculateWorkflow)
        {
            if (workflow.State != nameof(CaseLifecycleState.NotReady))
            {
                workflow.StateEnteredAtUtc = nowUtc;
            }
            workflow.State = nameof(CaseLifecycleState.NotReady);
            await CaseDueWorkScheduler.ScheduleAsync(
                db, workflow, snapshot.Work.Case.AcceptedInspectionDeadline, nowUtc, cancellationToken);
        }

        return evaluation;
    }
}
