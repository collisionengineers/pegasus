using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfActionLogQueries(IDbContextFactory<PegasusDbContext> contextFactory) : IActionLogQueries
{
    public async Task<ActionLogPage> ListAsync(ActionLogFilter filter, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.ActionHistory.AsNoTracking().Where(item =>
            item.OccurredAtUtc >= filter.FromUtc && item.OccurredAtUtc < filter.ToUtc);
        if (!string.IsNullOrWhiteSpace(filter.Actor)) query = query.Where(item => item.ActorSubjectId == filter.Actor);
        if (!string.IsNullOrWhiteSpace(filter.EventKind)) query = query.Where(item => item.EventKind == filter.EventKind);
        if (!string.IsNullOrWhiteSpace(filter.AggregateType)) query = query.Where(item => item.AggregateType == filter.AggregateType);
        if (!string.IsNullOrWhiteSpace(filter.Outcome)) query = query.Where(item => item.Outcome == filter.Outcome);
        if (!string.IsNullOrWhiteSpace(filter.CorrelationId)) query = query.Where(item => item.CorrelationId == filter.CorrelationId);
        var rows = await query.OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize + 1)
            .Select(item => new ActionLogRow(item.Id, item.AggregateType, item.AggregateId,
                item.EventKind, item.ActorKind, item.ActorSubjectId, item.OccurredAtUtc,
                item.Outcome, item.CorrelationId)).ToListAsync(cancellationToken);
        var hasMore = rows.Count > filter.PageSize;
        return new(hasMore ? rows[..filter.PageSize] : rows, hasMore);
    }
}
