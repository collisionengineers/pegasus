using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Automation activity read over the two existing append-only streams:
/// action history attributed to the Automation actor kind, and security
/// events recorded for automation ingress denials. No new store is
/// introduced.
/// </summary>
public sealed class EfAutomationActivityStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IAutomationActivityQueries
{
    public async Task<DateTimeOffset?> GetNewestActivityAtUtcAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var newestAction = await context.ActionHistory
            .AsNoTracking()
            .Where(item => item.ActorKind == nameof(ActorKind.Automation)
                && item.EventKind != EfRepairSpecificationStore.SourceHashReplayEventType)
            .MaxAsync(item => (DateTimeOffset?)item.OccurredAtUtc, cancellationToken);
        var newestSecurityEvent = await context.SecurityEvents
            .AsNoTracking()
            .Where(item => item.ReasonCode != null
                && item.ReasonCode.StartsWith(
                    AutomationActivityConventions.SecurityEventReasonPrefix))
            .MaxAsync(item => (DateTimeOffset?)item.OccurredAtUtc, cancellationToken);
        return newestAction > newestSecurityEvent || newestSecurityEvent is null
            ? newestAction
            : newestSecurityEvent;
    }
}
