using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

public sealed class InspectionAddressChoicesQueries(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IInspectionAddressChoicesQueries
{
    public async Task<IReadOnlyList<string>> GetPreviousAddressesAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var principalIds = context.Cases.Where(item => item.Id == caseId).Select(item => item.PrincipalId);
        var confirmed = await context.CaseDataFields.AsNoTracking()
            .Where(field => field.FieldName == CaseDataFieldNames.InspectionAddress
                && field.ValueKind == CaseDataCodes.Confirmed
                && field.ConfirmedAtUtc != null
                && field.Snapshot.Work.Kind == CaseWorkKinds.Primary
                && field.Snapshot.Work.CaseId != caseId
                && principalIds.Contains(field.Snapshot.Work.Case.PrincipalId)
                && field.Value != Ext18InspectionAddressPolicy.ImageBasedAssessment)
            .Select(field => new
            {
                field.Value,
                ConfirmedAtUtc = field.ConfirmedAtUtc!.Value
            })
            .ToListAsync(cancellationToken);

        // One entry per address, ignoring case and outer spaces, ordered by its
        // most recent confirmation.
        return confirmed
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Value))
            .GroupBy(candidate => candidate.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.ConfirmedAtUtc).First())
            .OrderByDescending(candidate => candidate.ConfirmedAtUtc)
            .Select(candidate => candidate.Value.Trim())
            .ToArray();
    }
}
