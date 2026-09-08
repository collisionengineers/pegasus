using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfActionLogQueries(IDbContextFactory<PegasusDbContext> contextFactory) : IActionLogQueries
{
    public async Task<ActionLogPage> ListAsync(ActionLogFilter filter, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var offset = (filter.Page - 1) * filter.PageSize;
        var actionQuery = context.ActionHistory.AsNoTracking().Where(item =>
            item.OccurredAtUtc >= filter.FromUtc && item.OccurredAtUtc < filter.ToUtc);
        var securityQuery = context.SecurityEvents.AsNoTracking().Where(item =>
            item.OccurredAtUtc >= filter.FromUtc && item.OccurredAtUtc < filter.ToUtc);

        if (filter.Area is { } area)
        {
            actionQuery = actionQuery.Where(item => item.AggregateType == area);
            securityQuery = area == "Security"
                ? securityQuery
                : securityQuery.Where(_ => false);
        }
        if (filter.Actor is { } actor)
        {
            actionQuery = actionQuery.Where(item => item.ActorSubjectId == actor);
            securityQuery = securityQuery.Where(item => item.SubjectId == actor);
        }
        if (filter.Result is { } result)
        {
            actionQuery = actionQuery.Where(item => item.Outcome == result);
            securityQuery = securityQuery.Where(item => item.Outcome == result);
        }
        if (filter.Operation is { } operation)
        {
            actionQuery = actionQuery.Where(item => item.EventKind == operation);
            securityQuery = securityQuery.Where(item => item.Type == operation);
        }
        if (filter.Record is { } record)
        {
            actionQuery = actionQuery.Where(item => item.AggregateId == record);
            securityQuery = securityQuery.Where(item => item.SubjectId == record);
        }
        if (filter.CorrelationId is { } correlationId)
        {
            actionQuery = actionQuery.Where(item => item.CorrelationId == correlationId);
            securityQuery = securityQuery.Where(item => item.CorrelationId == correlationId);
        }
        if (filter.SearchText is { } searchText)
        {
            actionQuery = actionQuery.Where(item =>
                item.AggregateType.Contains(searchText) || item.AggregateId.Contains(searchText)
                || item.EventKind.Contains(searchText) || item.ActorSubjectId.Contains(searchText)
                || item.Outcome.Contains(searchText) || item.CorrelationId.Contains(searchText));
            securityQuery = securityQuery.Where(item =>
                item.Type.Contains(searchText) || item.SubjectId.Contains(searchText)
                || item.Outcome.Contains(searchText) || item.CorrelationId.Contains(searchText)
                || (item.ReasonCode != null && item.ReasonCode.Contains(searchText)));
        }

        var actions = actionQuery
            .Select(item => new
            {
                item.Id,
                Area = item.AggregateType,
                Operation = item.EventKind,
                Reference = item.AggregateId,
                Actor = item.ActorSubjectId,
                item.OccurredAtUtc,
                Result = item.Outcome,
                item.CorrelationId
            });
        var securityEvents = securityQuery
            .Select(item => new
            {
                item.Id,
                Area = "Security",
                Operation = item.Type,
                Reference = item.SubjectId,
                Actor = item.SubjectId,
                item.OccurredAtUtc,
                Result = item.Outcome,
                item.CorrelationId
            });

        var rows = await (filter.OldestFirst
                ? actions.Concat(securityEvents).OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.Id)
                : actions.Concat(securityEvents).OrderByDescending(item => item.OccurredAtUtc).ThenByDescending(item => item.Id))
            .Skip(offset).Take(filter.PageSize + 1)
            .Select(item => new ActionLogRow(item.Id, item.Area, item.Operation,
                item.Reference, item.Actor, item.OccurredAtUtc, item.Result,
                item.CorrelationId))
            .ToArrayAsync(cancellationToken);
        var hasMore = rows.Length > filter.PageSize;
        return new(hasMore ? rows[..filter.PageSize] : rows, hasMore);
    }

}
