using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>Reads the salvage matrix of the Principal a Case belongs to.</summary>
public sealed class EfPrincipalSalvageMatrixQueries(
    IDbContextFactory<PegasusDbContext> contextFactory) : IPrincipalSalvageMatrixQueries
{
    public async Task<SalvageMatrix?> GetForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var json = await (from @case in context.Cases.AsNoTracking()
                          join principal in context.Principals.AsNoTracking() on @case.PrincipalId equals principal.Id
                          where @case.Id == caseId
                          select principal.SalvageMatrixJson)
            .SingleOrDefaultAsync(cancellationToken);
        return EfOrganizationAdministration.ReadSalvageMatrix(json);
    }
}
