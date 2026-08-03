using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Consolidated Automation activity read model over the two existing
/// append-only streams: action history attributed to the Automation actor
/// kind, and security events recorded for automation ingress denials. No new
/// store is introduced; the correlation identifier addresses one operation
/// across both streams.
/// </summary>
public sealed class EfAutomationActivityStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAutomationActivityQueries
{
    public async Task<ListAutomationActivityResult> ListAsync(
        ListAutomationActivityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var offset = (request.Page - 1) * request.PageSize;
        var window = checked(offset + request.PageSize + 1);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var actionQuery = context.ActionHistory
            .AsNoTracking()
            .Where(item => item.ActorKind == nameof(ActorKind.Automation));
        var securityQuery = context.SecurityEvents
            .AsNoTracking()
            .Where(item => item.ReasonCode != null
                && item.ReasonCode.StartsWith(
                    AutomationActivityConventions.SecurityEventReasonPrefix));
        if (request.CorrelationId is { } correlationId)
        {
            actionQuery = actionQuery.Where(item => item.CorrelationId == correlationId);
            securityQuery = securityQuery.Where(item => item.CorrelationId == correlationId);
        }

        var actions = await actionQuery
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(window)
            .Select(item => new AutomationActivityRecord(
                item.Id,
                AutomationActivityRecordType.ActionHistory,
                item.EventKind,
                item.ActorSubjectId,
                item.OccurredAtUtc,
                item.Outcome,
                item.CorrelationId,
                item.AggregateType,
                item.AggregateId,
                item.Reason))
            .ToArrayAsync(cancellationToken);
        var securityEvents = await securityQuery
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(window)
            .Select(item => new AutomationActivityRecord(
                item.Id,
                AutomationActivityRecordType.SecurityEvent,
                item.Type,
                item.SubjectId,
                item.OccurredAtUtc,
                item.Outcome,
                item.CorrelationId,
                null,
                null,
                item.ReasonCode))
            .ToArrayAsync(cancellationToken);

        var page = actions
            .Concat(securityEvents)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip(offset)
            .Take(request.PageSize + 1)
            .ToArray();
        var hasMore = page.Length > request.PageSize;
        return new(
            hasMore ? page[..request.PageSize] : page,
            request.CorrelationId,
            request.Page,
            request.PageSize,
            request.Page > 1,
            hasMore);
    }
}
