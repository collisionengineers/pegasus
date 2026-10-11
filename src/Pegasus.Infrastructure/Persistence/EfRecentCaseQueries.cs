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

    public async Task<RecentCasesPage> ListAsync(
        DateTimeOffset sinceUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        // The count and the page share nothing, nor do the facts and the
        // drafts, so each pair runs together on contexts of its own.
        var countTask = CountAsync(sinceUtc, cancellationToken);
        var pageTask = ReadPageAsync(sinceUtc, page, pageSize, cancellationToken);
        await Task.WhenAll(countTask, pageTask);
        var count = countTask.Result;
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

        return new RecentCasesPage(items, page, pageSize, count);
    }

    // New cases are definitive instructions; a Triage Case is counted by its
    // own Work Centre metric.
    private static IQueryable<CaseEntity> CreatedCases(PegasusDbContext context, DateTimeOffset sinceUtc)
    {
        var dismissals = context.Set<WorkCentreDismissalEntity>();
        return context.Set<CaseEntity>().AsNoTracking()
            .Where(caseEntity => caseEntity.CreatedAtUtc >= sinceUtc
                && caseEntity.Type != CaseTypeCodes.Triage
                && !dismissals.Any(dismissal => dismissal.RecordId == caseEntity.Id
                    && dismissal.DismissedAtUtc >= caseEntity.CreatedAtUtc));
    }

    private async Task<int> CountAsync(DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await CreatedCases(context, sinceUtc).CountAsync(cancellationToken);
    }

    private async Task<Row[]> ReadPageAsync(
        DateTimeOffset sinceUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var createdCases = CreatedCases(context, sinceUtc);

        // The reference is unique, so Cases created at one moment keep one
        // order on every page. A page past any int is past the end.
        var skipped = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var creationEvents = context.CaseWorkflowEvents.Where(item =>
            item.BeforeVersion == 0
            && item.AfterVersion == 0
            && (item.EventType == "manual_case_created"
                || item.EventType == "case_created_as_replacement"));
        return await (
            from caseEntity in createdCases
                .OrderByDescending(item => item.CreatedAtUtc)
                .ThenBy(item => item.Reference)
                .Skip(skipped)
                .Take(pageSize)
            join principal in context.Set<PrincipalEntity>().AsNoTracking()
                on caseEntity.PrincipalId equals principal.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            orderby caseEntity.CreatedAtUtc descending, caseEntity.Reference
            select new Row(
                caseEntity.Id,
                caseEntity.Reference,
                principal.Code,
                caseEntity.CreatedAtUtc,
                receipt == null ? null : receipt.SourceChannel,
                creationEvents.Any(item =>
                    item.CaseId == caseEntity.Id
                    && item.ActorKind == AutomationActorKind)))
            .ToArrayAsync(cancellationToken);
    }

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
