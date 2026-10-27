using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's New cases feed (D8): every Case created in the window,
/// whatever created it, newest first and paged. What the Automation actor
/// later does to a Case is its history, not a new Case (operator, 8 October
/// 2026). Arrival is the accepted receipt's channel; a Case with no receipt
/// was created by hand unless its creation event names the Automation actor.
/// A Case dismissed at or after its creation
/// (<see cref="WorkCentreDismissalPolicy"/>) is not listed or counted.
/// </summary>
internal sealed class EfRecentCaseQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IRecentCaseQueries
{
    private static readonly string AutomationActorKind = nameof(ActorKind.Automation);
    private static readonly string MailboxCode = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
    private static readonly string PrincipalApiCode = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.PrincipalApi);
    private static readonly string AutomationCode = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Automation);
    private static readonly string ManualUploadCode = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ManualUpload);

    public async Task<RecentCasesPage> ListAsync(
        DateTimeOffset sinceUtc,
        int page,
        int pageSize,
        CaseArrival? arrival,
        string? search,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        // The arrival tally and the page share nothing, nor do the facts and the
        // drafts, so each pair runs together on contexts of its own.
        var countsTask = ReadArrivalCountsAsync(sinceUtc, cancellationToken);
        var filteredCountTask = CountFilteredAsync(sinceUtc, arrival, search, cancellationToken);
        var pageTask = ReadPageAsync(sinceUtc, page, pageSize, arrival, search, cancellationToken);
        await Task.WhenAll(countsTask, filteredCountTask, pageTask);
        var counts = countsTask.Result;
        var pageRows = pageTask.Result;
        var caseIds = pageRows.Select(row => row.CaseId).ToArray();
        var factsTask = ReadFactsAsync(caseIds, cancellationToken);
        var draftsTask = ReadDraftsAsync(caseIds, cancellationToken);
        await Task.WhenAll(factsTask, draftsTask);
        var facts = factsTask.Result;
        var drafts = draftsTask.Result;

        string? Fact(Guid caseId, string field) =>
            facts.FirstOrDefault(item => item.CaseId == caseId && item.FieldName == field)?.Value;

        var items = pageRows.Select(row =>
        {
            var draft = drafts.FirstOrDefault(item => item.CaseId == row.CaseId);
            return new RecentCaseRow(
                row.CaseId,
                row.Reference,
                Fact(row.CaseId, CaseDataFieldNames.VehicleRegistration) ?? draft?.VehicleRegistration,
                Fact(row.CaseId, CaseDataFieldNames.ClaimantName) ?? draft?.ClaimantName,
                row.Principal,
                row.CreatedAtUtc,
                RecentCasesPolicy.Arrival(Channel(row.SourceChannel), row.CreatedByAutomation));
        }).ToArray();

        // The tally is of the whole window, before the arrival and search
        // filters, so every channel chip states what it would show. The page
        // total is the filtered list, which is what paging walks.
        return new RecentCasesPage(items, page, pageSize, filteredCountTask.Result)
        {
            ArrivalCounts = counts
        };
    }

    private async Task<int> CountFilteredAsync(
        DateTimeOffset sinceUtc,
        CaseArrival? arrival,
        string? search,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await Search(ArrivalFilter(ArrivedCases(context, sinceUtc), arrival), context, search)
            .CountAsync(cancellationToken);
    }

    // New cases are definitive instructions; a Triage Case is counted by its
    // own Work Centre metric.
    private static IQueryable<ArrivalRow> ArrivedCases(PegasusDbContext context, DateTimeOffset sinceUtc)
    {
        var dismissals = context.Set<WorkCentreDismissalEntity>();
        var creationEvents = context.CaseWorkflowEvents.Where(item =>
            item.BeforeVersion == 0
            && item.AfterVersion == 0
            && (item.EventType == "manual_case_created"
                || item.EventType == "case_created_as_replacement"));
        return
            from caseEntity in context.Set<CaseEntity>().AsNoTracking()
            where caseEntity.CreatedAtUtc >= sinceUtc
                && caseEntity.Type != CaseTypeCodes.Triage
                && !dismissals.Any(dismissal => dismissal.RecordId == caseEntity.Id
                    && dismissal.DismissedAtUtc >= caseEntity.CreatedAtUtc)
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            select new ArrivalRow
            {
                CaseId = caseEntity.Id,
                Reference = caseEntity.Reference,
                PrincipalId = caseEntity.PrincipalId,
                OriginIntakeReceiptId = caseEntity.OriginIntakeReceiptId,
                CreatedAtUtc = caseEntity.CreatedAtUtc,
                SourceChannel = receipt == null ? null : receipt.SourceChannel,
                CreatedByAutomation = creationEvents.Any(item =>
                    item.CaseId == caseEntity.Id
                    && item.ActorKind == AutomationActorKind)
            };
    }

    /// <summary>One statement: the window's Cases with the arrival each derives, tallied per channel.</summary>
    private async Task<IReadOnlyDictionary<CaseArrival, int>> ReadArrivalCountsAsync(
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ArrivedCases(context, sinceUtc)
            .Select(row => new { row.SourceChannel, row.CreatedByAutomation })
            .ToListAsync(cancellationToken);
        var counts = Enum.GetValues<CaseArrival>().ToDictionary(arrival => arrival, _ => 0);
        foreach (var row in rows)
        {
            counts[RecentCasesPolicy.Arrival(Channel(row.SourceChannel), row.CreatedByAutomation)]++;
        }

        return counts;
    }

    private async Task<Row[]> ReadPageAsync(
        DateTimeOffset sinceUtc,
        int page,
        int pageSize,
        CaseArrival? arrival,
        string? search,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var createdCases = Search(ArrivalFilter(ArrivedCases(context, sinceUtc), arrival), context, search);

        // The reference is unique, so Cases created at one moment keep one
        // order on every page. A page past any int is past the end.
        var skipped = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var rows =
            from row in createdCases
            join principal in context.Set<PrincipalEntity>().AsNoTracking()
                on row.PrincipalId equals principal.Id
            orderby row.CreatedAtUtc descending, row.Reference
            select new Row(
                row.CaseId,
                row.Reference,
                principal.Code,
                row.CreatedAtUtc,
                row.SourceChannel,
                row.CreatedByAutomation);
        return await rows.Skip(skipped).Take(pageSize).ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// The arrival sub-filter (v42 Design A), the same arms as
    /// <see cref="RecentCasesPolicy.Arrival"/>: a named channel wins, and a Case
    /// with no receipt — or a channel that is none of the four — is Manual or
    /// Automation by the actor that created it.
    /// </summary>
    private static IQueryable<ArrivalRow> ArrivalFilter(IQueryable<ArrivalRow> rows, CaseArrival? arrival) => arrival switch
    {
        CaseArrival.Email => rows.Where(row => row.SourceChannel == MailboxCode),
        CaseArrival.PrincipalApi => rows.Where(row => row.SourceChannel == PrincipalApiCode),
        // A null channel compares unknown in SQL, so the no-receipt arm is written
        // out rather than folded into an inequality.
        CaseArrival.Automation => rows.Where(row => row.SourceChannel == AutomationCode
            || (row.SourceChannel == null && row.CreatedByAutomation)
            || (row.SourceChannel != null
                && row.SourceChannel != MailboxCode
                && row.SourceChannel != PrincipalApiCode
                && row.SourceChannel != ManualUploadCode
                && row.CreatedByAutomation)),
        CaseArrival.Manual => rows.Where(row => row.SourceChannel == ManualUploadCode
            || (row.SourceChannel == null && !row.CreatedByAutomation)
            || (row.SourceChannel != null
                && row.SourceChannel != MailboxCode
                && row.SourceChannel != PrincipalApiCode
                && row.SourceChannel != AutomationCode
                && !row.CreatedByAutomation)),
        _ => rows
    };

    /// <summary>
    /// Find within New cases (<see cref="RecentCasesPolicy.Matches"/>): the
    /// reference, the claimant, or the registration with its spaces and dashes
    /// ignored, read from the confirmed facts or the Case's instruction draft.
    /// </summary>
    private static IQueryable<ArrivalRow> Search(
        IQueryable<ArrivalRow> rows,
        PegasusDbContext context,
        string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return rows;
        }

        var compact = Compact(term);
        var facts = context.CaseDataFields.AsNoTracking();
        var drafts = context.Set<InstructionDraftEntity>().AsNoTracking();
        return rows.Where(row =>
            row.Reference.Contains(term)
            || facts.Any(field => field.WorkId == row.CaseId
                && field.ValueKind == CaseDataCodes.Confirmed
                && (field.FieldName == CaseDataFieldNames.VehicleRegistration
                        && field.Value.Replace(" ", "").Replace("-", "").Contains(compact)
                    || field.FieldName == CaseDataFieldNames.ClaimantName
                        && field.Value.Contains(term)))
            || drafts.Any(draft => row.OriginIntakeReceiptId != null
                && draft.IntakeReceiptId == row.OriginIntakeReceiptId
                && (draft.VehicleRegistration != null
                        && draft.VehicleRegistration.Replace(" ", "").Replace("-", "").Contains(compact)
                    || draft.ClaimantName != null
                        && draft.ClaimantName.Contains(term))));
    }

    private static string Compact(string value) => value.Replace(" ", string.Empty).Replace("-", string.Empty);

    private async Task<List<FactRow>> ReadFactsAsync(Guid[] caseIds, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.CaseDataFields.AsNoTracking()
            .Where(item => caseIds.Contains(item.WorkId)
                && item.ValueKind == CaseDataCodes.Confirmed
                && (item.FieldName == CaseDataFieldNames.VehicleRegistration
                    || item.FieldName == CaseDataFieldNames.ClaimantName))
            .Select(item => new FactRow(item.WorkId, item.FieldName, item.Value))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<DraftRow>> ReadDraftsAsync(Guid[] caseIds, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
            from caseEntity in context.Set<CaseEntity>().AsNoTracking()
            join draft in context.Set<InstructionDraftEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals draft.IntakeReceiptId
            where caseIds.Contains(caseEntity.Id)
            select new DraftRow(caseEntity.Id, draft.VehicleRegistration, draft.ClaimantName))
            .ToListAsync(cancellationToken);
    }

    private static IntakeSourceChannel? Channel(string? code) => code switch
    {
        null => null,
        _ => EfIntakeReceiptStore.ParseSourceChannel(code)
    };

    private sealed record FactRow(Guid CaseId, string FieldName, string Value);

    private sealed record DraftRow(Guid CaseId, string? VehicleRegistration, string? ClaimantName);

    /// <summary>
    /// A Case created in the window with the arrival it derives, before the
    /// Principal is joined. Member-initialised, not constructed: the later
    /// filters, search and join read its members inside the same query, which
    /// the provider can only follow back through member bindings.
    /// </summary>
    private sealed class ArrivalRow
    {
        public required Guid CaseId { get; init; }
        public required string Reference { get; init; }
        public required Guid PrincipalId { get; init; }
        public required Guid? OriginIntakeReceiptId { get; init; }
        public required DateTimeOffset CreatedAtUtc { get; init; }
        public required string? SourceChannel { get; init; }
        public required bool CreatedByAutomation { get; init; }
    }

    private sealed record Row(
        Guid CaseId,
        string Reference,
        string Principal,
        DateTimeOffset CreatedAtUtc,
        string? SourceChannel,
        bool CreatedByAutomation);
}

/// <summary>The person's last look at the Work Centre, kept on their account row.</summary>
internal sealed class EfWorkCentreVisitStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IWorkCentreVisitStore
{
    public async Task<DateTimeOffset?> GetLastSeenAsync(Guid staffId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users.AsNoTracking()
            .Where(user => user.Id == staffId)
            .Select(user => user.WorkCentreLastSeenUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task MarkSeenAsync(Guid staffId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // A plain stamp: it is not a versioned edit of the account, so it neither
        // moves the account version nor contends with an administrator's save.
        await context.Users
            .Where(user => user.Id == staffId)
            .ExecuteUpdateAsync(set => set.SetProperty(user => user.WorkCentreLastSeenUtc, seenAtUtc), cancellationToken);
    }
}
