using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads the per-mailbox inbound-intake cursor rows so the administration surface can
/// say what each approved mailbox is doing. Read-only, and Web holds only SELECT on
/// this table, so approving a mailbox here still cannot start or stop a poll directly:
/// it changes the estate, and the next tick reads the estate.
/// </summary>
internal sealed class EfApprovedMailboxPollStatusQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IApprovedMailboxPollStatusQueries
{
    public async Task<IReadOnlyList<ApprovedMailboxPollStatus>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ApprovedInboxPollStates
            .AsNoTracking()
            .Include(item => item.ApprovedMailbox)
            .OrderBy(item => item.MailboxAddress)
            .ToListAsync(cancellationToken);
        var subscriptions = await context.ApprovedMailboxSubscriptions
            .AsNoTracking()
            .ToDictionaryAsync(item => item.ApprovedMailboxId, cancellationToken);
        return rows.Select(item =>
        {
            subscriptions.TryGetValue(item.ApprovedMailboxId, out var subscription);
            if (subscription?.Generation != item.Generation)
            {
                subscription = null;
            }
            var capabilities = new List<ApprovedMailboxRouteScope>(3);
            if (item.ApprovedMailbox.AllowInboundIntake)
            {
                capabilities.Add(ApprovedMailboxRouteScope.InboundIntake);
            }
            if (item.ApprovedMailbox.AllowSentEvidence)
            {
                capabilities.Add(ApprovedMailboxRouteScope.SentEvidence);
            }
            if (item.ApprovedMailbox.AllowStaffSend)
            {
                capabilities.Add(ApprovedMailboxRouteScope.StaffSend);
            }
            return new ApprovedMailboxPollStatus(
                item.ApprovedMailboxId,
                item.MailboxAddress,
                item.DueAtUtc,
                item.LastCompletedAtUtc,
                item.LastFailureCode,
                item.StartBoundaryUtc,
                item.Generation,
                subscription?.ExpiresAtUtc,
                subscription is null
                    ? null
                    : Enum.Parse<ApprovedMailboxSubscriptionLifecycleState>(subscription.LifecycleState),
                capabilities);
        }).ToArray();
    }
}
