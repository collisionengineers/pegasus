using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The dashboard's counts and the Work Centre's own reads, read straight from
/// the records that hold the fact.
/// </summary>
/// <remarks>
/// Each of these is a single aggregate query. The dashboard is the most
/// frequently loaded screen in the product, so none of them projects rows into
/// memory to count them.
/// </remarks>
internal sealed class EfDashboardQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IDashboardQueries
{
    private readonly IDbContextFactory<PegasusDbContext> contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var notReady = CaseLifecycleState.NotReady.ToString();
        var review = CaseLifecycleState.Review.ToString();
        var held = CaseLifecycleState.Held.ToString();
        var reportPreparation = CaseLifecycleState.ReportPreparation.ToString();
        var postReport = CaseLifecycleState.PostReport.ToString();
        var query = CaseLifecycleState.Query.ToString();

        var counts = await context.CaseWorkflows
            .AsNoTracking()
            .Where(workflow =>
                workflow.State == notReady
                || workflow.State == review
                || workflow.State == held
                || workflow.State == reportPreparation
                || workflow.State == postReport
                || workflow.State == query)
            .GroupBy(workflow => workflow.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        int For(string state) =>
            counts.SingleOrDefault(item => item.State == state)?.Count ?? 0;

        // Awaiting instruction has no CaseWorkflows row until it merges. Its
        // count mirrors Cases/Index.cshtml.cs LoadAwaitingAsync: the lifecycle
        // is AwaitingInstruction and the origin receipt has no current case
        // association. A reversed manual association overrides an older
        // accepted link in the same way as EfImageIntakeStore.ProjectAsync.
        var awaitingInstruction = EfImageIntakeStore.ToCode(ImageInitiatedCaseState.AwaitingInstruction);
        var awaitingInstructionCount = await context.ImageIntakes
            .AsNoTracking()
            .CountAsync(
                item => item.LifecycleState == awaitingInstruction
                    && !context.IntakeManualAssociations.Any(association =>
                        association.IntakeReceiptId == item.OriginReceiptId && association.IsActive)
                    && (context.IntakeManualAssociations.Any(association =>
                            association.IntakeReceiptId == item.OriginReceiptId)
                        || !context.CaseIntakeLinks.Any(link =>
                            link.IntakeReceiptId == item.OriginReceiptId)),
                cancellationToken);

        return new(
            For(notReady),
            For(review),
            For(held),
            For(reportPreparation) + For(postReport),
            awaitingInstructionCount,
            For(query));
    }

    public async Task<IReadOnlyList<PairedVehicleImagesCase>> ListPairedVehicleImagesAwaitingStaffAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        const string pairedEvent = "image_initiated_case_merged";
        var staff = nameof(ActorKind.Staff);
        var eligibleStates = EfImageIntakeCaseCandidates.EligibleStates;
        // The pairing is the Case's latest merge event; any staff change that
        // moved the Case version past it means someone has taken the Case up.
        // Notes, previews and downloads move no version and system work is
        // not staff, so neither clears it. Versions order a Case's changes
        // exactly, where two events can share an instant.
        var cases = await (
            from workflow in context.CaseWorkflows.AsNoTracking()
            join caseEntity in context.Cases.AsNoTracking() on workflow.CaseId equals caseEntity.Id
            let pairedVersion = context.CaseWorkflowEvents
                .Where(item => item.CaseId == workflow.CaseId && item.EventType == pairedEvent)
                .Max(item => (long?)item.AfterVersion)
            let pairedAt = context.CaseWorkflowEvents
                .Where(item => item.CaseId == workflow.CaseId && item.EventType == pairedEvent
                    && item.AfterVersion == pairedVersion)
                .Max(item => (DateTimeOffset?)item.OccurredAtUtc)
            where eligibleStates.Contains(workflow.State)
                && workflow.ReportSentEvidenceId == null
                && workflow.ArchivedAtUtc == null
                && pairedAt != null
                && !context.CaseWorkflowEvents.Any(item => item.CaseId == workflow.CaseId
                    && item.ActorKind == staff
                    && item.AfterVersion > item.BeforeVersion
                    && item.AfterVersion > pairedVersion)
            select new
            {
                workflow.CaseId,
                caseEntity.Reference,
                Principal = caseEntity.Principal.Code,
                workflow.AssignedEngineerId,
                PairedAtUtc = pairedAt!.Value
            }).ToArrayAsync(cancellationToken);
        if (cases.Length == 0)
        {
            return [];
        }

        var caseIds = cases.Select(item => item.CaseId).ToArray();
        var merged = EfImageIntakeStore.ToCode(ImageInitiatedCaseState.MergedIntoInstructionCase);
        var images = await context.ImageIntakes.AsNoTracking()
            .Where(item => item.LifecycleState == merged
                && item.MergedIntoCaseId != null
                && caseIds.Contains(item.MergedIntoCaseId.Value))
            .Select(item => new
            {
                CaseId = item.MergedIntoCaseId!.Value,
                item.ImageIntakeReference,
                item.CreatedAtUtc,
                MergedAtUtc = context.ImageIntakeLifecycleEvents
                    .Where(lifecycle => lifecycle.ImageIntakeId == item.Id && lifecycle.EventType == merged)
                    .Max(lifecycle => (DateTimeOffset?)lifecycle.OccurredAtUtc)
            })
            .ToArrayAsync(cancellationToken);
        var latest = images
            .GroupBy(item => item.CaseId)
            .ToDictionary(group => group.Key, group => group.MaxBy(item => item.MergedAtUtc)!);
        return cases
            .Where(item => latest.ContainsKey(item.CaseId))
            .Select(item => new PairedVehicleImagesCase(
                item.CaseId,
                item.Reference,
                latest[item.CaseId].ImageIntakeReference,
                item.Principal,
                item.AssignedEngineerId,
                item.PairedAtUtc,
                latest[item.CaseId].CreatedAtUtc))
            .ToArray();
    }

    public async Task<IReadOnlyList<OpenCaseTask>> ListOpenCaseTasksAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var open = nameof(CaseTaskState.Open);
        // A task records neither its creation instant nor its creator; both are
        // on its case_task_created history, written in the transaction that
        // added the task, whose aggregate id is the task id in "D" form matched
        // under the database's own collation. The Case's vehicle and claimant
        // are read as the Case search reads them: the instruction draft, else
        // the confirmed Case data.
        var rows = await (
            from task in context.CaseTasks.AsNoTracking()
            where task.State == open
            join created in context.ActionHistory.AsNoTracking()
                    .Where(item => item.AggregateType == "case_task" && item.EventKind == "case_task_created")
                on task.Id.ToString() equals created.AggregateId
            join caseEntity in context.Cases.AsNoTracking() on task.CaseId equals caseEntity.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            join draftCandidate in context.Set<InstructionDraftEntity>().AsNoTracking()
                on receipt.Id equals draftCandidate.IntakeReceiptId into drafts
            from draft in drafts.DefaultIfEmpty()
            join confirmedClaimantCandidate in context.CaseDataFields.AsNoTracking()
                    .Where(item => item.FieldName == CaseDataFieldNames.ClaimantName
                        && item.ValueKind == CaseDataCodes.Confirmed)
                on caseEntity.Id equals confirmedClaimantCandidate.WorkId into confirmedClaimants
            from confirmedClaimant in confirmedClaimants.DefaultIfEmpty()
            join confirmedRegistrationCandidate in context.CaseDataFields.AsNoTracking()
                    .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration
                        && item.ValueKind == CaseDataCodes.Confirmed)
                on caseEntity.Id equals confirmedRegistrationCandidate.WorkId into confirmedRegistrations
            from confirmedRegistration in confirmedRegistrations.DefaultIfEmpty()
            join confirmedMakeCandidate in context.CaseDataFields.AsNoTracking()
                    .Where(item => item.FieldName == CaseDataFieldNames.VehicleMake
                        && item.ValueKind == CaseDataCodes.Confirmed)
                on caseEntity.Id equals confirmedMakeCandidate.WorkId into confirmedMakes
            from confirmedMake in confirmedMakes.DefaultIfEmpty()
            join confirmedModelCandidate in context.CaseDataFields.AsNoTracking()
                    .Where(item => item.FieldName == CaseDataFieldNames.VehicleModel
                        && item.ValueKind == CaseDataCodes.Confirmed)
                on caseEntity.Id equals confirmedModelCandidate.WorkId into confirmedModels
            from confirmedModel in confirmedModels.DefaultIfEmpty()
            select new
            {
                task.CaseId,
                caseEntity.Reference,
                Principal = caseEntity.Principal.Code,
                Registration = draft == null ? confirmedRegistration!.Value : draft.VehicleRegistration,
                Claimant = draft == null ? confirmedClaimant!.Value : draft.ClaimantName,
                VehicleMake = draft == null ? confirmedMake!.Value : draft.VehicleMake,
                VehicleModel = draft == null ? confirmedModel!.Value : draft.VehicleModel,
                TaskId = task.Id,
                task.Description,
                task.AssigneeId,
                CreatedAtUtc = created.OccurredAtUtc,
                CreatedByKind = created.ActorKind,
                CreatedBySubjectId = created.ActorSubjectId
            }).ToArrayAsync(cancellationToken);
        var staff = nameof(ActorKind.Staff);
        return rows
            .Select(row => new OpenCaseTask(
                row.CaseId,
                row.Reference,
                row.Principal,
                row.Registration,
                row.Claimant,
                row.VehicleMake,
                row.VehicleModel,
                row.TaskId,
                row.Description,
                row.AssigneeId,
                row.CreatedAtUtc,
                row.CreatedByKind == staff && Guid.TryParse(row.CreatedBySubjectId, out var staffId)
                    ? (Guid?)staffId
                    : null))
            .ToArray();
    }
}
