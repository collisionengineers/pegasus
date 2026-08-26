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
                item.WorkItem.FailureCode,
                AcceptedCaseId = context.CaseIntakeLinks
                    .Where(link => link.IntakeReceiptId == item.WorkItem.ProcessedReceiptId)
                    .Select(link => (Guid?)link.CaseId)
                    .FirstOrDefault(),
                ManualAssociation = context.IntakeManualAssociations
                    .Where(association => association.IntakeReceiptId == item.WorkItem.ProcessedReceiptId)
                    .Select(association => new { association.CaseId, association.IsActive })
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (staged is null)
        {
            return null;
        }

        var workState = EfIntakeWorkStore.ParseState(staged.State);
        var caseId = staged.ManualAssociation is null
            ? staged.AcceptedCaseId
            : staged.ManualAssociation.IsActive
                ? staged.ManualAssociation.CaseId
                : null;
        return new(
            staged.Id,
            staged.SourceFileName,
            staged.ReceivedAtUtc,
            QueuedIntakeStatusKinds.FromWorkState(workState),
            staged.ProcessedReceiptId,
            caseId,
            staged.FailureCode,
            workState == IntakeWorkState.RetryScheduled ? staged.DueAtUtc : null);
    }
}
