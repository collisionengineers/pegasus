using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// MI-02's periods: per Principal and London month, the reports first sent,
/// the re-sends, and the fees frozen in the reports those first sends
/// carried, each with its Audit share (R673). Reads the same sends as the
/// per-Principal report so the two agree.
/// </summary>
internal sealed class EfMonthlyReportActivityQueries(
    IDbContextFactory<PegasusDbContext> factory) : IMonthlyReportActivityQueries
{
    public async Task<IReadOnlyList<MonthlyReportActivity>> GetAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var sends = await ReportSends.InPeriodAsync(db, fromUtc, toUtc, cancellationToken);
        var fees = await FrozenReportSnapshot.ReadAgreedFeesAsync(
            db,
            sends.Where(send => send.IsFirst).Select(send => send.GenerationId).ToHashSet(),
            cancellationToken);

        return sends
            .GroupBy(send => (send.PrincipalId, send.PrincipalCode, Month: MonthOf(send.SentAtUtc)))
            .Select(group =>
            {
                var first = group.Where(send => send.IsFirst).ToList();
                var resends = group.Where(send => !send.IsFirst).ToList();
                return new MonthlyReportActivity(
                    group.Key.PrincipalId,
                    group.Key.PrincipalCode,
                    group.Key.Month.Year,
                    group.Key.Month.Month,
                    first.Count,
                    resends.Count,
                    first.Sum(send => fees[send.GenerationId]),
                    first.Count(send => send.IsAudit),
                    resends.Count(send => send.IsAudit),
                    first.Where(send => send.IsAudit).Sum(send => fees[send.GenerationId]));
            })
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .ThenBy(x => x.PrincipalCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (int Year, int Month) MonthOf(DateTimeOffset instant)
    {
        var local = LondonCalendar.DateAt(instant);
        return (local.Year, local.Month);
    }
}
