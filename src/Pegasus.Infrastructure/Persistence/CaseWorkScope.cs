using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Resolves which work of a Case a read or write addresses, inside the
/// caller's own context and transaction. The current work is the Audit work
/// once one exists, else the primary work (whose id is the Case id). Writers
/// resolve it after their version, lease, archive and lock guards: Create
/// audit takes the same workflow lock and bumps the version, so a write
/// against the old current work is refused as stale.
/// </summary>
internal static class CaseWorkScope
{
    public static async Task<Guid> CurrentIdAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        // Tracked, so a row this context adds to the work is fixed up to it
        // and its Work navigation names the Case before it is saved.
        var works = await context.CaseWorks
            .Where(work => work.CaseId == caseId)
            .ToListAsync(cancellationToken);
        return works.SingleOrDefault(work => work.Kind == CaseWorkKinds.Audit)?.Id ?? caseId;
    }

    public static Task<Guid> ResolveIdAsync(
        PegasusDbContext context,
        Guid caseId,
        CaseWorkSelector selector,
        CancellationToken cancellationToken) =>
        selector == CaseWorkSelector.Primary
            ? Task.FromResult(caseId)
            : CurrentIdAsync(context, caseId, cancellationToken);

    /// <summary>
    /// The selected work of one Case as a query, so a read can filter on it
    /// inside its own command rather than resolve the id with another.
    /// </summary>
    public static IQueryable<Guid> SelectedIds(
        PegasusDbContext context,
        Guid caseId,
        CaseWorkSelector selector) =>
        selector == CaseWorkSelector.Primary
            ? context.CaseWorks.Where(work => work.Id == caseId).Select(work => work.Id)
            : CurrentWorkIds(context.CaseWorks.Where(work => work.CaseId == caseId), context);

    /// <summary>
    /// The current work of every Case, for joins: each Case's Audit work where
    /// it has one, else its primary work.
    /// </summary>
    public static IQueryable<Guid> CurrentWorkIds(PegasusDbContext context) =>
        CurrentWorkIds(context.CaseWorks, context);

    private static IQueryable<Guid> CurrentWorkIds(IQueryable<CaseWorkEntity> works, PegasusDbContext context) =>
        works
            .Where(work => work.Kind == CaseWorkKinds.Audit
                || !context.CaseWorks.Any(audit =>
                    audit.CaseId == work.CaseId && audit.Kind == CaseWorkKinds.Audit))
            .Select(work => work.Id);

    public static async Task<CaseWorkSet> LoadSetAsync(
        PegasusDbContext context,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var rows = await context.CaseWorks.AsNoTracking()
            .Include(work => work.ReportApproval)
            .Include(work => work.ReportSentEvidence)
            .Where(work => work.CaseId == caseId)
            .ToListAsync(cancellationToken);
        var primary = rows.SingleOrDefault(work => work.Kind == CaseWorkKinds.Primary)
            ?? throw new InvalidDataException($"Case '{caseId}' has no primary work.");
        var audit = rows.SingleOrDefault(work => work.Kind == CaseWorkKinds.Audit);
        return new(Map(primary), audit is null ? null : Map(audit));
    }

    public static CaseWork Map(CaseWorkEntity entity) => new(
        entity.Id,
        entity.CaseId,
        CaseWorkKinds.Parse(entity.Kind),
        entity.CreatedAtUtc,
        entity.ReportApproval is null ? null : EfCaseWorkflowStore.MapReportApproval(entity.ReportApproval),
        entity.ReportSentEvidence is null ? null : EfCaseWorkflowStore.MapReportSentEvidence(entity.ReportSentEvidence));
}
