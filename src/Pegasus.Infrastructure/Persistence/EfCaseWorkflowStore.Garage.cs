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
            || workflow.ArchivedAtUtc is not null
            || workflow.State != nameof(CaseLifecycleState.NotReady)
            || workflow.EditLeaseExpiresAtUtc > now)
        {
            return false;
        }

        var garage = await CaseGarage.ReadAsync(
            context, workflow.Case, workflow.GarageAuthorisedAtUtc, cancellationToken);
        var beforeJson = JsonSerializer.Serialize(HistoryValue(workflow));
        var beforeVersion = workflow.Version;
        string eventType;
        string reason;
        if (GarageAuthorisationPolicy.WaitIsOver(CaseLifecycleState.NotReady, garage))
        {
            workflow.State = nameof(CaseLifecycleState.ReportPreparation);
            CaseChaseState.Stop(workflow);
            eventType = GarageAuthorisationPolicy.ReturnedEventType;
            reason = garage.RepairInvoiceFiled
                ? GarageAuthorisationPolicy.RepairInvoiceFiledReason
                : GarageAuthorisationPolicy.OutcomeWaivedReason;
        }
        else if (garage is { Applies: true, Authorised: false, ExternalEstimateFiled: true }
            && workflow.AssignedEngineerId is null)
        {
            var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: true)
                .SingleOrDefaultAsync(item => item.WorkId == caseId, cancellationToken);
            if (snapshot is null)
            {
                return false;
            }

            // Asked before anything is written: a Case still missing its
            // mileage, say, is not rescheduled every run.
            var configuration = await EfWorkflowConfigurationStore.ReadAsync(context, cancellationToken);
            var ready = CaseCompletenessPolicy.Evaluate(
                new CaseCompleteness(workflow.Case.InstructionComplete, workflow.Case.ImagesComplete),
                configuration,
                CaseInstructionCompleteness.VehicleGate(snapshot, CaseLifecycleState.NotReady, garage));
            if (!ready.SatisfiesPolicy)
            {
                return false;
            }

            await CaseInstructionCompleteness.ApplyReadinessAsync(
                context, workflow, snapshot, now, cancellationToken);
            if (workflow.State != nameof(CaseLifecycleState.Review))
            {
                return false;
            }

            eventType = GarageAuthorisationPolicy.RequirementsCompletedEventType;
            reason = GarageAuthorisationPolicy.ExternalEstimateFiledReason;
        }
        else
        {
            return false;
        }

        workflow.StateEnteredAtUtc = now;
        workflow.Version = checked(workflow.Version + 1);
        var afterJson = JsonSerializer.Serialize(HistoryValue(workflow));
        AddEvent(
            context,
            workflow,
            GarageWaitActor,
            $"garage-wait:{beforeVersion}",
            reason,
            Hash($"{eventType}|{caseId:D}|{beforeVersion}"),
            eventType,
            beforeVersion,
            workflow.Version,
            now,
            beforeJson,
            afterJson);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
