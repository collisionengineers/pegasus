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
        return await context.ApprovedInboxPollStates
            .AsNoTracking()
            .OrderBy(item => item.MailboxAddress)
            .Select(item => new ApprovedMailboxPollStatus(
                item.ApprovedMailboxId,
                item.MailboxAddress,
                item.DueAtUtc,
                item.LastCompletedAtUtc,
                item.LastFailureCode))
            .ToListAsync(cancellationToken);
    }
}
