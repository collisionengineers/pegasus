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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        // New cases are definitive instructions; a Triage Case is counted by
        // its own Work Centre metric.
        var dismissals = context.Set<WorkCentreDismissalEntity>();
        var createdCases = context.Set<CaseEntity>().AsNoTracking()
            .Where(caseEntity => caseEntity.CreatedAtUtc >= sinceUtc
                && caseEntity.Type != CaseTypeCodes.Triage
                && !dismissals.Any(dismissal => dismissal.RecordId == caseEntity.Id
                    && dismissal.DismissedAtUtc >= caseEntity.CreatedAtUtc));
        var count = await createdCases.CountAsync(cancellationToken);

        // The reference is unique, so Cases created at one moment keep one
        // order on every page. A page past any int is past the end.
        var skipped = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var creationEvents = context.CaseWorkflowEvents.Where(item =>
            item.BeforeVersion == 0
            && item.AfterVersion == 0
            && (item.EventType == "manual_case_created"
                || item.EventType == "case_created_as_replacement"));
        var pageRows = await (
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
        var caseIds = pageRows.Select(row => row.CaseId).ToArray();
        var facts = await context.CaseDataFields.AsNoTracking()
            .Where(item => caseIds.Contains(item.WorkId)
                && item.ValueKind == CaseDataCodes.Confirmed
                && (item.FieldName == CaseDataFieldNames.VehicleRegistration
                    || item.FieldName == CaseDataFieldNames.ClaimantName))
            .Select(item => new { CaseId = item.WorkId, item.FieldName, item.Value })
            .ToListAsync(cancellationToken);
        var drafts = await (
            from caseEntity in context.Set<CaseEntity>().AsNoTracking()
            join draft in context.Set<InstructionDraftEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals draft.IntakeReceiptId
            where caseIds.Contains(caseEntity.Id)
            select new { CaseId = caseEntity.Id, draft.VehicleRegistration, draft.ClaimantName })
            .ToListAsync(cancellationToken);

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

    private static IntakeSourceChannel? Channel(string? code) => code switch
    {
        null => null,
        _ => EfIntakeReceiptStore.ParseSourceChannel(code)
    };

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
