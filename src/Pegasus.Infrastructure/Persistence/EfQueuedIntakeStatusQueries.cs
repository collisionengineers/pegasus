using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfQueuedIntakeStatusQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IQueuedIntakeStatusQueries
{
    public async Task<QueuedIntakeStatus?> GetAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var staged = await context.IntakeStagedReceipts
            .AsNoTracking()
            .Where(item => item.Id == stagedReceiptId)
            .Select(item => new
            {
                item.Id,
                item.SourceFileName,
                item.ReceivedAtUtc,
                State = item.WorkItem!.State,
                item.WorkItem.DueAtUtc,
                item.WorkItem.ProcessedReceiptId,
                item.WorkItem.FailureCode
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (staged is null)
        {
            return null;
        }

        // The due time is a retry fact only: every other state carries a due
        // time meaning something else (the next dispatch sweep, the lease
        // expiry), which no surface should read as "this cannot move yet".
        var workState = EfIntakeWorkStore.ParseState(staged.State);
        return new(
            staged.Id,
            staged.SourceFileName,
            staged.ReceivedAtUtc,
            QueuedIntakeStatusKinds.FromWorkState(workState),
            staged.ProcessedReceiptId,
            staged.FailureCode,
            workState == IntakeWorkState.RetryScheduled ? staged.DueAtUtc : null);
    }
}
