using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Reads a receipt's search documents in ordinal order: the body row first,
/// then one per attachment. The rows are written by intake; this reads only.
/// </summary>
internal sealed class EfIntakeSearchDocumentReader(
    IDbContextFactory<PegasusDbContext> contextFactory) : IIntakeSearchDocumentReader
{
    public async Task<IReadOnlyList<IntakeSearchDocument>> ReadAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<IntakeSearchDocumentEntity>()
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == intakeReceiptId)
            .OrderBy(item => item.Ordinal)
            .Select(item => new IntakeSearchDocument(
                item.SourceLabel,
                item.AttachmentFileName,
                item.Text,
                item.AttachmentOrdinal))
            .ToListAsync(cancellationToken);
    }
}
