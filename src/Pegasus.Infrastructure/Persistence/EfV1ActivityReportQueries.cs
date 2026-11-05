using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads each Principal's report activity from its durable facts (MI-02,
/// MI-03, MI-05; R673): the report sends (<see cref="ReportSends"/>), each
/// report counted once on its first send and every later send a re-send, the
/// fee frozen in the report each first send carried, and the queues now.
/// </summary>
internal sealed class EfV1ActivityReportQueries(
    IDbContextFactory<PegasusDbContext> factory) : IV1ActivityReportQueries
{
    public async Task<IReadOnlyList<PrincipalReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var sends = await ReportSends.InPeriodAsync(db, fromUtc, toUtc, cancellationToken);

        // MI-02: each report's fee is the one frozen in the report its first
        // send carried (an Inspection + Audit Case's Audit carries its own
        // fee), counted in the period of that send.
        var fees = await FrozenReportSnapshot.ReadAgreedFeesAsync(
            db,
            sends.Where(send => send.IsFirst).Select(send => send.GenerationId).ToHashSet(),
            cancellationToken);

        // Triage's three non-terminal persisted states are its current queue;
        // CreatedAtUtc is the durable age anchor, never inferred from history.
        // A Triage Case's Principal is its Case's.
        var triage = await db.Set<TriageEntity>().AsNoTracking()
            .Where(x => x.State != "completed" && x.State != "cancelled")
            .Select(x => new TriageRow(x.Case.PrincipalId, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var held = await (
            from workflow in db.CaseWorkflows.AsNoTracking()
            join @case in db.Cases.AsNoTracking() on workflow.CaseId equals @case.Id
            where workflow.State == nameof(CaseLifecycleState.Held)
            select new HeldRow(
                @case.PrincipalId,
                db.CaseWorkflowEvents.AsNoTracking()
                    .Where(eventRow => eventRow.CaseId == workflow.CaseId
                        && eventRow.EventType == "case_held")
                    .OrderByDescending(eventRow => eventRow.OccurredAtUtc)
                    .Select(eventRow => (DateTimeOffset?)eventRow.OccurredAtUtc)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var operationalPrincipalIds = triage.Select(x => x.PrincipalId)
            .Concat(held.Select(x => x.PrincipalId))
            .Distinct()
            .ToArray();
        var operationalPrincipalCodes = operationalPrincipalIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Principals.AsNoTracking()
                .Where(principal => operationalPrincipalIds.Contains(principal.Id))
                .ToDictionaryAsync(principal => principal.Id, principal => principal.Code, cancellationToken);

        return sends.Select(x => new PrincipalKey(x.PrincipalId, x.PrincipalCode))
            .Concat(triage.Select(row => new PrincipalKey(
                row.PrincipalId,
                operationalPrincipalCodes[row.PrincipalId])))
            .Concat(held.Select(row => new PrincipalKey(
                row.PrincipalId,
                operationalPrincipalCodes[row.PrincipalId])))
            .Distinct()
            .Select(key => BuildRow(
                key,
                [.. sends.Where(x => x.PrincipalId == key.PrincipalId)],
                [.. triage.Where(x => x.PrincipalId == key.PrincipalId)],
                [.. held.Where(x => x.PrincipalId == key.PrincipalId)],
                fees))
            .OrderBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PrincipalId)
            .ToList();
    }

    private static PrincipalReportActivity BuildRow(
        PrincipalKey key,
        List<ReportSendFact> sends,
        List<TriageRow> triage,
        List<HeldRow> held,
        Dictionary<Guid, decimal> fees)
    {
        var first = sends.Where(send => send.IsFirst).ToList();
        var resends = sends.Where(send => !send.IsFirst).ToList();
        // MI-03: the time to each report's first send, from the instruction's
        // receipt or, for an Inspection + Audit Case's Audit, from Create audit.
        var sentDurations = first
            .Where(send => send.TurnaroundStartUtc is not null)
            .Select(send => send.SentAtUtc - send.TurnaroundStartUtc!.Value)
            .ToList();
        return new(
            key.PrincipalId,
            key.Code,
            first.Count,
            resends.Count,
            Average(sentDurations),
            triage.Count,
            triage.Select(x => (DateTimeOffset?)x.CreatedAtUtc).Min(),
            held.Count,
            held.Select(x => x.HeldAtUtc).Min(),
            held.Count(x => x.HeldAtUtc is null),
            first.Sum(send => fees[send.GenerationId]),
            first.Count(send => send.IsAudit),
            resends.Count(send => send.IsAudit),
            first.Where(send => send.IsAudit).Sum(send => fees[send.GenerationId]));
    }

    private static TimeSpan? Average(List<TimeSpan> durations) => durations.Count == 0
        ? null
        : TimeSpan.FromTicks((long)durations.Average(x => x.Ticks));

    private sealed record PrincipalKey(Guid PrincipalId, string Code);
    private sealed record TriageRow(Guid PrincipalId, DateTimeOffset CreatedAtUtc);
    private sealed record HeldRow(Guid PrincipalId, DateTimeOffset? HeldAtUtc);
}
