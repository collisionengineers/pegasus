using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The garage authorisation milestone of a Garage authorisation and invoice
/// Audit (operator, 17 November 2026; R1141 to R1144, R1159, R1161): the two
/// staff acts, and the reconciliation that moves a waiting Case on when a file
/// or an Outcome has ended its wait.
/// </summary>
public sealed partial class EfCaseWorkflowStore : IGarageAuthorisationStore, IGarageWaitStore
{
    private static readonly ActionActor GarageWaitActor = ActionActor.SystemWorker("garage-wait");

    /// <summary>The pages of candidates one reconciliation run reads.</summary>
    private const int GarageWaitPagesPerRun = 4;

    public Task<CaseWorkflowRecord> AuthoriseGarageAsync(
        AuthoriseGarageRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(request, GarageAuthorisationPolicy.AuthorisedEventType, async (context, workflow, now) =>
        {
            var garage = await CaseGarage.ReadAsync(
                context, workflow.Case, workflow.GarageAuthorisedAtUtc, cancellationToken);
            workflow.GarageAuthorisedAtUtc = now;
            if (GarageAuthorisationPolicy.WaitsAfterAuthorisation(garage))
            {
                // The Case keeps its Engineer and is chased for the invoice on
                // the standard schedule (R1142, R1144).
                workflow.State = nameof(CaseLifecycleState.NotReady);
                await CaseDueWorkScheduler.ScheduleAsync(
                    context,
                    workflow,
                    workflow.Case.AcceptedInspectionDeadline,
                    now,
                    cancellationToken,
                    GarageAuthorisationPolicy.AwaitingRepairInvoice);
            }
        },
        cancellationToken,
        refuseAsync: async (context, workflow, token) =>
            GarageAuthorisationPolicy.RequireCanAuthorise(
                StateOf(workflow),
                workflow.AssignedEngineerId,
                await CaseGarage.ReadAsync(context, workflow.Case, workflow.GarageAuthorisedAtUtc, token)));

    public Task<CaseWorkflowRecord> UndoGarageAuthorisationAsync(
        UndoGarageAuthorisationRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(request, GarageAuthorisationPolicy.UndoneEventType, async (context, workflow, now) =>
        {
            workflow.GarageAuthorisedAtUtc = null;
            if (workflow.State == nameof(CaseLifecycleState.NotReady))
            {
                // The Case was waiting for the invoice: it returns to With
                // Engineer, keeping its Engineer, or Unassigned when it has
                // none or the account is no longer enabled (R1159).
                workflow.State = nameof(CaseLifecycleState.ReportPreparation);
                CaseChaseState.Stop(workflow);
                if (workflow.AssignedEngineerId is { } engineerId
                    && await EfCaseEngineerEligibility.ReadAsync(context, engineerId, cancellationToken)
                        is not { AccountExists: true, IsEnabled: true })
                {
                    workflow.AssignedEngineerId = null;
                }
            }
        },
        cancellationToken,
        refuseAsync: async (context, workflow, token) =>
            GarageAuthorisationPolicy.RequireCanUndo(
                StateOf(workflow),
                workflow.ArchivedAtUtc is not null,
                await CaseGarage.ReadAsync(context, workflow.Case, workflow.GarageAuthorisedAtUtc, token)));

    public async Task<int> ReconcileAsync(int limit, CancellationToken cancellationToken)
    {
        // A candidate its lock then leaves alone (its mileage is still
        // missing, say) is passed over, so it never keeps the Cases after it
        // from their turn: a run reads on, a bounded number of pages.
        var moved = 0;
        Guid? after = null;
        for (var page = 0; page < GarageWaitPagesPerRun; page++)
        {
            IReadOnlyList<Guid> candidates;
            await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
            {
                candidates = await CaseGarage.CandidatesAsync(
                    context, timeProvider.GetUtcNow(), after, limit, cancellationToken);
            }

            foreach (var caseId in candidates)
            {
                if (await StoreConcurrencyRetry.ExecuteAsync(
                        token => ReconcileOnceAsync(caseId, token), cancellationToken))
                {
                    moved++;
                }
            }

            if (candidates.Count < limit)
            {
                break;
            }

            after = candidates[^1];
        }

        return moved;
    }

    /// <summary>
    /// One Case, under its workflow lock, from the facts as they stand now: a
    /// candidate that has since moved, been archived or been opened for
    /// editing is left alone.
    /// </summary>
    private async Task<bool> ReconcileOnceAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await AcquireWorkflowMutationLockAsync(context, caseId, cancellationToken);
        var workflow = await WorkflowQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (workflow is null
            || workflow.EditLeaseExpiresAtUtc > now
            || !await EndGarageWaitAsync(context, workflow, GarageWaitActor, null, now, cancellationToken))
        {
            return false;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The same move when a staged upload's External estimate or Repair
    /// invoice is filed in custody, under the Case's workflow lock and inside
    /// the filing's transaction: the upload moves the Case when its file is
    /// filed, not on the next sweep (R1177). A Case somebody is editing is
    /// left to the sweep, so the session's version does not move under it.
    /// </summary>
    internal static async Task EndGarageWaitOnFilingAsync(
        PegasusDbContext context,
        Guid caseId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var workflow = await WorkflowQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (workflow is not null
            && !(workflow.EditLeaseExpiresAtUtc > now)
            && await EndGarageWaitAsync(context, workflow, GarageWaitActor, null, now, cancellationToken))
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Moves a Not ready Garage authorisation and invoice Audit on when its
    /// stored facts end its wait, and records the move as a line of its own
    /// in the Case's history: the one decision the Worker's sweep and the
    /// staff acts that file the fact share. The upload, Change type and the
    /// Outcome save move the Case in the same save; the sweep moves it for
    /// every other route a file or an Outcome arrives by (operator,
    /// 18 November 2026; R1177). Facts are read from the database, so the
    /// caller saves its own write first, inside the same transaction.
    /// </summary>
    /// <param name="workflow">The tracked workflow, with its Case, under the workflow lock.</param>
    /// <param name="actor">Who made the move: the member of staff whose act ended the wait, or the sweep.</param>
    /// <param name="sharedFromVersion">
    /// The version an upload started at when the move shares the one version
    /// an upload takes, as an image's completion does; null when the move
    /// takes a version of its own (R997).
    /// </param>
    /// <returns>Whether the Case moved.</returns>
    internal static async Task<bool> EndGarageWaitAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        ActionActor actor,
        long? sharedFromVersion,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Any other Case is not this owner's, and costs no read.
        if (workflow.ArchivedAtUtc is not null
            || workflow.State != nameof(CaseLifecycleState.NotReady)
            || !CaseGarage.Applies(workflow.Case))
        {
            return false;
        }

        var beforeJson = JsonSerializer.Serialize(HistoryValue(workflow));
        var move = await MoveOnFromNotReadyAsync(context, workflow, now, cancellationToken);
        if (move.Kind == NotReadyMove.None)
        {
            return false;
        }

        var (eventType, reason) = move.Kind == NotReadyMove.ReturnedToEngineer
            ? (GarageAuthorisationPolicy.ReturnedEventType,
                move.Garage.RepairInvoiceFiled
                    ? GarageAuthorisationPolicy.RepairInvoiceFiledReason
                    : GarageAuthorisationPolicy.OutcomeWaivedReason)
            : (GarageAuthorisationPolicy.RequirementsCompletedEventType,
                GarageAuthorisationPolicy.ExternalEstimateFiledReason);
        workflow.StateEnteredAtUtc = now;
        long beforeVersion;
        if (sharedFromVersion is { } shared)
        {
            beforeVersion = shared;
        }
        else
        {
            beforeVersion = workflow.Version;
            CaseMutationGuard.Advance(workflow);
        }

        var afterJson = JsonSerializer.Serialize(HistoryValue(workflow));
        AddEvent(
            context,
            workflow,
            actor,
            $"garage-wait:{beforeVersion}",
            reason,
            Hash($"{eventType}|{workflow.CaseId:D}|{beforeVersion}"),
            eventType,
            beforeVersion,
            workflow.Version,
            now,
            beforeJson,
            afterJson);
        return true;
    }

    private enum NotReadyMove
    {
        None,
        ReturnedToEngineer,
        IntoReview
    }

    /// <summary>
    /// Applies a Not ready Case's stored facts to its state, writing no
    /// history: a garage wait that is over returns the Case to With Engineer
    /// (R1143, R1144), and a Case with no Engineer that lacks nothing enters
    /// Review (FRD-13; R1141 on a garage Audit). Readiness is asked before
    /// anything is written, so a Case still missing its mileage, say, keeps
    /// its chase as it stands.
    /// </summary>
    private static async Task<(NotReadyMove Kind, CaseGarageFacts Garage)> MoveOnFromNotReadyAsync(
        PegasusDbContext context,
        CaseWorkflowEntity workflow,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var garage = await CaseGarage.ReadAsync(
            context, workflow.Case, workflow.GarageAuthorisedAtUtc, cancellationToken);
        if (GarageAuthorisationPolicy.WaitIsOver(CaseLifecycleState.NotReady, garage))
        {
            // The Case returns with its Engineer whatever the state of that
            // account: the sweep's role cannot read staff accounts, and a With
            // Engineer Case keeps a disabled Engineer too (R1163, amending R1144).
            // The chase to stop is the Case's stored one, which not every
            // caller has loaded.
            var dueWork = context.Entry(workflow).Reference(item => item.DueWork);
            if (!dueWork.IsLoaded)
            {
                await dueWork.LoadAsync(cancellationToken);
            }

            workflow.State = nameof(CaseLifecycleState.ReportPreparation);
            CaseChaseState.Stop(workflow);
            return (NotReadyMove.ReturnedToEngineer, garage);
        }

        if (workflow.AssignedEngineerId is not null)
        {
            return (NotReadyMove.None, garage);
        }

        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: true)
            .SingleOrDefaultAsync(item => item.WorkId == workflow.CaseId, cancellationToken);
        if (snapshot is null)
        {
            return (NotReadyMove.None, garage);
        }

        var configuration = await EfWorkflowConfigurationStore.ReadAsync(context, cancellationToken);
        var ready = CaseCompletenessPolicy.Evaluate(
            new CaseCompleteness(workflow.Case.InstructionComplete, workflow.Case.ImagesComplete),
            configuration,
            CaseInstructionCompleteness.VehicleGate(snapshot, CaseLifecycleState.NotReady, garage));
        if (!ready.SatisfiesPolicy)
        {
            return (NotReadyMove.None, garage);
        }

        await CaseInstructionCompleteness.ApplyReadinessAsync(
            context, workflow, snapshot, now, cancellationToken);
        return workflow.State == nameof(CaseLifecycleState.Review)
            ? (NotReadyMove.IntoReview, garage)
            : (NotReadyMove.None, garage);
    }
}
