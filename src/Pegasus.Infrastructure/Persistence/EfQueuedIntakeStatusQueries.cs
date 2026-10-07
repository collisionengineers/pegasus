using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfQueuedIntakeStatusQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IQueuedIntakeStatusQueries
{
    public Task<QueuedIntakeStatus?> GetAsync(
        Guid stagedReceiptId,
        CancellationToken cancellationToken = default) =>
        FindAsync(item => item.Id == stagedReceiptId, cancellationToken);

    public Task<QueuedIntakeStatus?> FindBySourceIdentityAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceIdentity);
        var channel = EfIntakeReceiptStore.ToCode(sourceIdentity.Channel);
        return FindAsync(
            item => item.SourceChannel == channel
                && item.ExternalReceiptToken == sourceIdentity.ExternalReceiptToken,
            cancellationToken);
    }

    private async Task<QueuedIntakeStatus?> FindAsync(
        Expression<Func<IntakeStagedReceiptEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var staged = await context.IntakeStagedReceipts
            .AsNoTracking()
            .Where(predicate)
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
