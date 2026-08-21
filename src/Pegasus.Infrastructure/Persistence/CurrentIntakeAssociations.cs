using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed record CurrentIntakeAssociation(Guid CaseId, string Reference);

/// <summary>
/// A receipt's association as it stands now and, just as importantly,
/// whether one once stood and was deliberately taken away. A reversed
/// association is not the same as never having had one: the automatic
/// allocation attempt still names the case it created, so without this
/// distinction an unlinked message goes on reporting the very link the
/// operator just removed (INTK-029).
/// </summary>
internal sealed record IntakeAssociations(
    IReadOnlyDictionary<Guid, CurrentIntakeAssociation> Current,
    IReadOnlySet<Guid> ReversedReceiptIds)
{
    /// <summary>
    /// Whether the automatic allocation's own record of the case it created
    /// may still stand in for a missing association. It may not once that
    /// association has been reversed: the reversal is the operator saying
    /// this message is not that case's.
    /// </summary>
    public bool AllocationMayStandIn(Guid receiptId) =>
        !ReversedReceiptIds.Contains(receiptId);
}

internal static class CurrentIntakeAssociations
{
    internal static async Task<IntakeAssociations> ReadAsync(
        PegasusDbContext context,
        IReadOnlyCollection<Guid> receiptIds,
        CancellationToken cancellationToken)
    {
        if (receiptIds.Count == 0)
        {
            return new(new Dictionary<Guid, CurrentIntakeAssociation>(), new HashSet<Guid>());
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

        var reversed = manual
            .Where(item => !item.IsActive)
            .Select(item => item.IntakeReceiptId)
            .Where(receiptId => !current.ContainsKey(receiptId))
            .ToHashSet();
        return new(current, reversed);
    }
}
