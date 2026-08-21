using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed record CurrentIntakeAssociation(Guid CaseId, string Reference);

internal static class CurrentIntakeAssociations
{
    internal static async Task<IReadOnlyDictionary<Guid, CurrentIntakeAssociation>> ReadAsync(
        PegasusDbContext context,
        IReadOnlyCollection<Guid> receiptIds,
        CancellationToken cancellationToken)
    {
        if (receiptIds.Count == 0)
        {
            return new Dictionary<Guid, CurrentIntakeAssociation>();
        }

        var ids = receiptIds.Distinct().ToArray();
        var manual = await context.IntakeManualAssociations
            .AsNoTracking()
            .Where(item => ids.Contains(item.IntakeReceiptId))
            .Select(item => new
            {
                item.IntakeReceiptId,
                item.IsActive,
                item.CaseId,
                item.Case.Reference
            })
            .ToListAsync(cancellationToken);
        var manualReceiptIds = manual.Select(item => item.IntakeReceiptId).ToHashSet();
        var current = manual
            .Where(item => item.IsActive)
            .ToDictionary(
                item => item.IntakeReceiptId,
                item => new CurrentIntakeAssociation(item.CaseId, item.Reference));

        var accepted = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => ids.Contains(item.IntakeReceiptId)
                && !manualReceiptIds.Contains(item.IntakeReceiptId))
            .Select(item => new
            {
                item.IntakeReceiptId,
                item.CaseId,
                item.Case.Reference
            })
            .ToListAsync(cancellationToken);
        foreach (var item in accepted)
        {
            current[item.IntakeReceiptId] = new(item.CaseId, item.Reference);
        }

        return current;
    }
}
