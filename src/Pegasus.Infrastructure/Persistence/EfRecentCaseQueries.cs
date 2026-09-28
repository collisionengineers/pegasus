using System.Linq.Expressions;
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

    /// <summary>The event that created its Case, which the feed shows as the New case row.</summary>
    private static readonly Expression<Func<CaseWorkflowEventEntity, bool>> IsCreationEvent =
        item => item.BeforeVersion == 0
            && item.AfterVersion == 0
            && (item.EventType == "manual_case_created"
                || item.EventType == "case_created_as_replacement");

    private static readonly Expression<Func<CaseWorkflowEventEntity, bool>> IsNotCreationEvent =
        Expression.Lambda<Func<CaseWorkflowEventEntity, bool>>(
            Expression.Not(IsCreationEvent.Body),
            IsCreationEvent.Parameters);

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
        var createdCases = context.Set<CaseEntity>().AsNoTracking()
            .Where(caseEntity => caseEntity.CreatedAtUtc >= sinceUtc
                && caseEntity.Type != CaseTypeCodes.Triage);
        // A creation event is already its Case's New case row, and guidance
        // applied at version 0 is part of creating the Case.
        var changes = context.CaseWorkflowEvents.AsNoTracking()
            .Where(IsNotCreationEvent)
            .Where(change => change.OccurredAtUtc >= sinceUtc
                && change.ActorKind == AutomationActorKind
                && !(change.EventType == "case_guidance_applied" && change.BeforeVersion == 0));
        // Every Case has its Principal and every event its Case, so the joins
        // below neither add nor drop rows and each source is counted alone.
        var createdCount = await createdCases.CountAsync(cancellationToken);
        var changedCount = await changes.CountAsync(cancellationToken);

        // Only a source's newest page*pageSize rows can reach the requested
        // page. A longer source is cut at its page*pageSize-th newest moment,
        // keeping every row tied with it, so the cut is the same whatever
        // order ties come back in and the merge below orders exactly the
        // rows that lead the whole window. A page past any int is past the end.
        var leading = (int)Math.Min((long)page * pageSize, int.MaxValue);
        var skipped = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var createdWindow = createdCases;
        if (createdCount > leading)
        {
            createdWindow = createdCases.Where(caseEntity => caseEntity.CreatedAtUtc
                >= createdCases
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .Skip(leading - 1)
                    .Select(item => item.CreatedAtUtc)
                    .FirstOrDefault());
        }

        var changedWindow = changes;
        if (changedCount > leading)
        {
            changedWindow = changes.Where(change => change.OccurredAtUtc
                >= changes
                    .OrderByDescending(item => item.OccurredAtUtc)
                    .Skip(leading - 1)
                    .Select(item => item.OccurredAtUtc)
                    .FirstOrDefault());
        }

        var creationEvents = context.CaseWorkflowEvents.Where(IsCreationEvent);
        var created = await (
            from caseEntity in createdWindow
            join principal in context.Set<PrincipalEntity>().AsNoTracking()
                on caseEntity.PrincipalId equals principal.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
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
                null,
                null))
            .ToListAsync(cancellationToken);

        var changed = await (
            from change in changedWindow
            join caseEntity in context.Set<CaseEntity>().AsNoTracking()
                on change.CaseId equals caseEntity.Id
            join principal in context.Set<PrincipalEntity>().AsNoTracking()
                on caseEntity.PrincipalId equals principal.Id
            join receiptCandidate in context.Set<IntakeReceiptEntity>().AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals receiptCandidate.Id into receipts
            from receipt in receipts.DefaultIfEmpty()
            select new Row(
                RecentCaseRowKind.ChangedByAutomation,
                caseEntity.Id,
                caseEntity.Reference,
                principal.Code,
                change.OccurredAtUtc,
                receipt == null ? null : receipt.SourceChannel,
                false,
                change.EventType,
                change.Id))
            .ToListAsync(cancellationToken);

        // Rows at the same moment on the same Case fall back to the New case
        // row, then event identity, so every page agrees on where ties go.
        var rows = created.Concat(changed)
            .OrderByDescending(row => row.OccurredAtUtc)
            .ThenBy(row => row.Reference, StringComparer.Ordinal)
            .ThenBy(row => row.Kind)
            .ThenBy(row => row.EventId)
            .ToArray();
        var pageRows = rows.Skip(skipped).Take(pageSize).ToArray();
        var caseIds = pageRows.Select(row => row.CaseId).Distinct().ToArray();
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

        return new RecentCasesPage(items, page, pageSize, createdCount + changedCount);
    }

    private static IntakeSourceChannel? Channel(string? code) => code switch
    {
        null => null,
        _ => EfIntakeReceiptStore.ParseSourceChannel(code)
    };

    private sealed record Row(
        RecentCaseRowKind Kind,
        Guid CaseId,
        string Reference,
        string Principal,
        DateTimeOffset OccurredAtUtc,
        string? SourceChannel,
        bool CreatedByAutomation,
        string? ChangeKind,
        Guid? EventId);
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
