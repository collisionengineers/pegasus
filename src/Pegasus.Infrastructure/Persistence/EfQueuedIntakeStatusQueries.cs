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
                item.WorkItem.ProcessedReceiptId,
                item.WorkItem.FailureCode,
                CaseId = context.CaseIntakeLinks
                    .Where(link => link.IntakeReceiptId == item.WorkItem.ProcessedReceiptId)
                    .Select(link => (Guid?)link.CaseId)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (staged is null)
        {
            return null;
        }

        return new(
            staged.Id,
            staged.SourceFileName,
            staged.ReceivedAtUtc,
            QueuedIntakeStatusKinds.FromWorkState(EfIntakeWorkStore.ParseState(staged.State)),
            staged.ProcessedReceiptId,
            staged.CaseId,
            staged.FailureCode);
    }
}
