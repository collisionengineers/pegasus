using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Work Centre's New cases feed (D8): every Case created in the window,
/// whatever created it, and every change the Automation actor made to an
/// existing Case in the same window, newest first and paged. Arrival is the
/// accepted receipt's channel; a Case with no receipt was created by hand
/// unless its first workflow event names the Automation actor.
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

        var creationEvents = CreationEvents(context.CaseWorkflowEvents);
        var created = await (
            from caseEntity in context.Set<CaseEntity>().AsNoTracking()
            join principal in context.Set<PrincipalEntity>().AsNoTracking()
                on caseEntity.PrincipalId equals principal.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            where caseEntity.CreatedAtUtc >= sinceUtc
                && caseEntity.Type != "triage"
            select new Row(
                RecentCaseRowKind.NewCase,
                caseEntity.Id,
                caseEntity.Reference,
                principal.Code,
                caseEntity.CreatedAtUtc,
                receipt == null ? null : receipt.SourceChannel,
                creationEvents.Any(item =>
                    item.CaseId == caseEntity.Id
                    && item.ActorKind == AutomationActorKind),
                null))
            .ToListAsync(cancellationToken);

        var changed = await (
            from change in context.CaseWorkflowEvents.AsNoTracking()
            join caseEntity in context.Set<CaseEntity>().AsNoTracking()
                on change.CaseId equals caseEntity.Id
            join principal in context.Set<PrincipalEntity>().AsNoTracking()
                on caseEntity.PrincipalId equals principal.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            where change.OccurredAtUtc >= sinceUtc
                && change.ActorKind == AutomationActorKind
                && !creationEvents.Select(item => item.Id).Contains(change.Id)
                && !(change.EventType == "case_guidance_applied" && change.BeforeVersion == 0)
            select new Row(
                RecentCaseRowKind.ChangedByAutomation,
                caseEntity.Id,
                caseEntity.Reference,
                principal.Code,
                change.OccurredAtUtc,
                receipt == null ? null : receipt.SourceChannel,
                false,
                change.EventType))
            .ToListAsync(cancellationToken);

        var rows = created.Concat(changed)
            .OrderByDescending(row => row.OccurredAtUtc)
            .ThenBy(row => row.Reference, StringComparer.Ordinal)
            .ToArray();
        var pageRows = rows.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var caseIds = pageRows.Select(row => row.CaseId).Distinct().ToArray();
        var facts = await context.CaseDataFields.AsNoTracking()
            .Where(item => caseIds.Contains(item.CaseId)
                && item.ValueKind == CaseDataCodes.Confirmed
                && (item.FieldName == CaseDataFieldNames.VehicleRegistration
                    || item.FieldName == CaseDataFieldNames.ClaimantName))
            .Select(item => new { item.CaseId, item.FieldName, item.Value })
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
                row.Kind,
                row.CaseId,
                row.Reference,
                Fact(row.CaseId, CaseDataFieldNames.VehicleRegistration) ?? draft?.VehicleRegistration,
                Fact(row.CaseId, CaseDataFieldNames.ClaimantName) ?? draft?.ClaimantName,
                row.Principal,
                row.OccurredAtUtc,
                RecentCasesPolicy.Arrival(Channel(row.SourceChannel), row.CreatedByAutomation),
                row.ChangeKind);
        }).ToArray();

        return new RecentCasesPage(items, page, pageSize, rows.Length);
    }

    private static IntakeSourceChannel? Channel(string? code) => code switch
    {
        null => null,
        _ => EfIntakeReceiptStore.ParseSourceChannel(code)
    };

    private static IQueryable<CaseWorkflowEventEntity> CreationEvents(
        IQueryable<CaseWorkflowEventEntity> events) =>
        events.Where(item => item.BeforeVersion == 0
            && item.AfterVersion == 0
            && (item.EventType == "manual_case_created"
                || item.EventType == "case_created_as_replacement"
                || item.EventType == "audit_case_created"));

    private sealed record Row(
        RecentCaseRowKind Kind,
        Guid CaseId,
        string Reference,
        string Principal,
        DateTimeOffset OccurredAtUtc,
        string? SourceChannel,
        bool CreatedByAutomation,
        string? ChangeKind);
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
