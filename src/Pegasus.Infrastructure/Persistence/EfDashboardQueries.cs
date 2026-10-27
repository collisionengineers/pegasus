using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Lifecycle;
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

    /// <summary>
    /// The states of the Case queues other than Queries, which is the
    /// open-query mark on any open state.
    /// </summary>
    private static readonly string[] QueueStates =
    [
        nameof(CaseLifecycleState.NotReady),
        nameof(CaseLifecycleState.Review),
        nameof(CaseLifecycleState.Held),
        nameof(CaseLifecycleState.ReportPreparation)
    ];

    public async Task<CaseStageCounts> GetCaseStageCountsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // One statement: every open Case by state and open-query mark. Query is
        // a queue of open Cases with an open query, whatever their state
        // (operator, 11 October 2026).
        var counts = await OpenCaseWorkflows(context)
            .GroupBy(workflow => new { workflow.State, workflow.HasOpenQuery })
            .Select(group => new { group.Key.State, group.Key.HasOpenQuery, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        return StageCounts(counts.Select(item => (item.State, item.HasOpenQuery, item.Count)).ToArray());
    }

    public async Task<CaseQueues> GetCaseQueuesAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // The counts' one statement, grouped by each Case's Principal as well,
        // so the Principals on the Case queues (v41 U3) cost the join and no
        // second round trip.
        var counts = await (
            from workflow in OpenCaseWorkflows(context)
            join caseEntity in context.Cases.AsNoTracking() on workflow.CaseId equals caseEntity.Id
            join principal in context.Principals.AsNoTracking() on caseEntity.PrincipalId equals principal.Id
            group workflow by new { workflow.State, workflow.HasOpenQuery, principal.Code } into grouped
            select new { grouped.Key.State, grouped.Key.HasOpenQuery, grouped.Key.Code, Count = grouped.Count() })
            .ToArrayAsync(cancellationToken);
        return new(
            StageCounts(counts.Select(item => (item.State, item.HasOpenQuery, item.Count)).ToArray()),
            counts
                .Where(item => item.HasOpenQuery || QueueStates.Contains(item.State))
                .Select(item => item.Code)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private static IQueryable<CaseWorkflowEntity> OpenCaseWorkflows(PegasusDbContext context)
    {
        var terminal = CaseLifecycleRules.TerminalStateNames();
        return context.CaseWorkflows
            .AsNoTracking()
            .Where(workflow => !terminal.Contains(workflow.State));
    }

    private static CaseStageCounts StageCounts((string State, bool HasOpenQuery, int Count)[] counts)
    {
        int For(CaseLifecycleState state) =>
            counts.Where(item => item.State == state.ToString()).Sum(item => item.Count);

        return new(
            For(CaseLifecycleState.NotReady),
            For(CaseLifecycleState.Review),
            For(CaseLifecycleState.Held),
            For(CaseLifecycleState.ReportPreparation),
            counts.Where(item => item.HasOpenQuery).Sum(item => item.Count));
    }

    public async Task<int> GetAwaitingInstructionCountAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // Awaiting instruction has no CaseWorkflows row until it merges. Its
        // count mirrors Cases/Index.cshtml.cs LoadAwaitingAsync: the lifecycle
        // is AwaitingInstruction and the origin receipt has no current case
        // association. A reversed manual association overrides an older
        // accepted link in the same way as EfImageIntakeStore.ProjectAsync.
        var awaitingInstruction = EfImageIntakeStore.ToCode(ImageInitiatedCaseState.AwaitingInstruction);
        return await context.ImageIntakes
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
        // The Case's vehicle and claimant are read as the Case search reads
        // them: the instruction draft, else the confirmed Case data.
        var rows = await (
            from task in context.CaseTasks.AsNoTracking()
            where task.State == open
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
                task.CreatedAtUtc,
                task.CreatedByStaffId
            }).ToArrayAsync(cancellationToken);
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
                row.CreatedByStaffId))
            .ToArray();
    }

    public async Task<IReadOnlyList<OpenQueryAttentionRow>> ListOpenQueriesForAttentionAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
            from query in context.CaseQueries.AsNoTracking()
            where query.ClosedAtUtc == null
            join workflow in context.CaseWorkflows.AsNoTracking() on query.CaseId equals workflow.CaseId
            where workflow.ArchivedAtUtc == null
            join caseEntity in context.Cases.AsNoTracking() on query.CaseId equals caseEntity.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            join draftCandidate in context.Set<InstructionDraftEntity>().AsNoTracking()
                on receipt.Id equals draftCandidate.IntakeReceiptId into drafts
            from draft in drafts.DefaultIfEmpty()
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
                query.Id,
                query.CaseId,
                caseEntity.Reference,
                Registration = draft == null ? confirmedRegistration!.Value : draft.VehicleRegistration,
                VehicleMake = draft == null ? confirmedMake!.Value : draft.VehicleMake,
                VehicleModel = draft == null ? confirmedModel!.Value : draft.VehicleModel,
                query.Details,
                query.IntakeReceiptId,
                query.AssigneeStaffId,
                query.RaisedAtUtc
            }).ToArrayAsync(cancellationToken);
        var receiptIds = rows
            .Where(row => row.IntakeReceiptId is not null)
            .Select(row => row.IntakeReceiptId!.Value)
            .Distinct()
            .ToArray();
        Dictionary<Guid, string> subjects = receiptIds.Length == 0
            ? []
            : await (
                from receipt in context.IntakeReceipts.AsNoTracking()
                where receiptIds.Contains(receipt.Id)
                join message in context.RetainedMailboxMessages.AsNoTracking()
                    on receipt.ExternalReceiptToken equals message.ExternalReceiptToken
                select new { receipt.Id, message.Subject })
                .ToDictionaryAsync(item => item.Id, item => item.Subject, cancellationToken);
        return rows
            .Select(row => new OpenQueryAttentionRow(
                row.Id,
                row.CaseId,
                row.Reference,
                row.Registration,
                row.VehicleMake,
                row.VehicleModel,
                Title: row.IntakeReceiptId is { } receiptId && subjects.TryGetValue(receiptId, out var subject) && !string.IsNullOrWhiteSpace(subject)
                    ? subject
                    : row.Details ?? row.Reference,
                row.AssigneeStaffId,
                row.RaisedAtUtc))
            .ToArray();
    }
}
