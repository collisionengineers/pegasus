using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;

namespace Pegasus.Infrastructure.Persistence;

public sealed class InspectionAddressChoicesQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IInspectionAddressChoicesQueries
{
    public async Task<InspectionAddressChoicesData?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        if (current is null)
        {
            return null;
        }

        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleAsync(item => item.CaseId == caseId, cancellationToken);
        var projection = EfCaseDataStore.Map(current, workflow);
        var principalId = current.Case.PrincipalId;

        var candidates = await context.CaseDataFields.AsNoTracking()
            .Where(field => field.FieldName == CaseDataFieldNames.InspectionAddress
                && field.ValueKind == CaseDataCodes.Confirmed
                && field.CaseId != caseId
                && field.Snapshot.Case.PrincipalId == principalId
                && field.Value != Ext18InspectionAddressPolicy.ImageBasedAssessment)
            .Select(field => new
            {
                field.Value,
                ConfirmedAtUtc = field.ConfirmedAtUtc!.Value
            })
            .ToListAsync(cancellationToken);

        var previousAddresses = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Value))
            .GroupBy(candidate => candidate.Value.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.ConfirmedAtUtc).First())
            .OrderByDescending(candidate => candidate.ConfirmedAtUtc)
            .Select(candidate => candidate.Value.Trim())
            .ToArray();

        return new(
            projection.Claimant.Address.Current?.Value,
            projection.Inspection.RepairerAddress?.Current?.Value,
            projection.Inspection.StorageLocation?.Current?.Value,
            previousAddresses);
    }
}
