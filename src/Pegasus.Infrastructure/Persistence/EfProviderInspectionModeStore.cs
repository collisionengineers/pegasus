using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfProviderInspectionModeStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IProviderInspectionModeStore
{
    public async Task<CaseInspectionMode?> GetForPrincipalAsync(
        string principalCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);
        var normalized = principalCode.Trim().ToUpperInvariant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mode = await context.Principals
            .AsNoTracking()
            .Where(item => item.Code == normalized && item.IsActive)
            .Select(item => item.InspectionMode)
            .SingleOrDefaultAsync(cancellationToken);
        return mode is null ? null : ProviderInspectionModePolicy.Parse(mode);
    }
}
