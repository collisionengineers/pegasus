using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;
using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// A principal's EVA submission settings, read the way
/// <see cref="EfProviderInspectionModeStore"/> reads its inspection mode
/// (EXT-04, following ADR-0018).
/// </summary>
public sealed class EfEvaSubmissionModeStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IEvaSubmissionModeStore
{
    public async Task<EvaSubmissionModes> GetForPrincipalAsync(
        string principalCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);
        var normalized = principalCode.Trim().ToUpperInvariant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var policy = await context.Principals
            .AsNoTracking()
            .Where(item => item.Code == normalized && item.IsActive)
            .Select(item => item.ReportGenerationPolicy)
            .SingleOrDefaultAsync(cancellationToken);

        // A code naming no active principal has enabled nothing. Returning
        // Disabled rather than null keeps every caller on one branch: there is
        // no difference between "switched off" and "no such principal" that a
        // submission decision needs to act on, and inventing one would give a
        // replaced principal a route its successor controls.
        return policy is null
            ? EvaSubmissionModes.Disabled
            : new(Enum.Parse<PrincipalReportGenerationPolicy>(policy));
    }
}
