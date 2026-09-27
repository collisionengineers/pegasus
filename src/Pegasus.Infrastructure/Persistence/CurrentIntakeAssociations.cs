using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Case a receipt stands linked to. <see cref="IsTriage"/> is read from
/// the Case's own type, so a link to a Triage Case says so whichever route
/// made it.
/// </summary>
internal sealed record CurrentIntakeAssociation(Guid CaseId, string Reference, bool IsTriage);

/// <summary>
/// A receipt's association as it stands now and, just as importantly,
/// whether one once stood and was deliberately taken away. A reversed
/// association is not the same as never having had one: the automatic
/// allocation attempt still names the case it created, so without this
/// distinction an unlinked message goes on reporting the very link the
/// operator just removed.
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
                item.Case.Reference,
                IsTriage = item.Case.Type == CaseTypeCodes.Triage
            })
            .ToListAsync(cancellationToken);
        var manualReceiptIds = manual.Select(item => item.IntakeReceiptId).ToHashSet();
        var current = manual
            .Where(item => item.IsActive)
            .ToDictionary(
                item => item.IntakeReceiptId,
                item => new CurrentIntakeAssociation(item.CaseId, item.Reference, item.IsTriage));

        var accepted = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => ids.Contains(item.IntakeReceiptId)
                && !manualReceiptIds.Contains(item.IntakeReceiptId))
            .Select(item => new
            {
                item.IntakeReceiptId,
                item.CaseId,
                item.Case.Reference,
                IsTriage = item.Case.Type == CaseTypeCodes.Triage
            })
            .ToListAsync(cancellationToken);
        foreach (var item in accepted)
        {
            current[item.IntakeReceiptId] = new(item.CaseId, item.Reference, item.IsTriage);
        }

        var reversed = manual
            .Where(item => !item.IsActive)
            .Select(item => item.IntakeReceiptId)
            .Where(receiptId => !current.ContainsKey(receiptId))
            .ToHashSet();
        return new(current, reversed);
    }
}
