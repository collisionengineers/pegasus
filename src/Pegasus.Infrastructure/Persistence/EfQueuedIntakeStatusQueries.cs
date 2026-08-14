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
                item.WorkItem.FailureCode
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (staged is null)
        {
            return null;
        }

        Guid? caseId = null;
        if (staged.ProcessedReceiptId is { } processedReceiptId)
        {
            caseId = await context.CaseIntakeLinks
                .AsNoTracking()
                .Where(link => link.IntakeReceiptId == processedReceiptId)
                .Select(link => (Guid?)link.CaseId)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new(
            staged.Id,
            staged.SourceFileName,
            staged.ReceivedAtUtc,
            Map(staged.State),
            staged.ProcessedReceiptId,
            caseId,
            staged.FailureCode);
    }

    private static QueuedIntakeStatusKind Map(string state) => state switch
    {
        "pending" or "dispatching" or "dispatched" or "retry_scheduled" =>
            QueuedIntakeStatusKind.Received,
        "processing" => QueuedIntakeStatusKind.Processing,
        "completed" => QueuedIntakeStatusKind.Complete,
        "failed" => QueuedIntakeStatusKind.Failed,
        _ => throw new InvalidDataException($"Unknown persisted intake work state '{state}'.")
    };
}
