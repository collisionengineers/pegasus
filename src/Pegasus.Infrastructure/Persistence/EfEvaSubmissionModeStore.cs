using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;

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
        var modes = await context.Principals
            .AsNoTracking()
            .Where(item => item.Code == normalized && item.IsActive)
            .Select(item => new EvaSubmissionModes(
                item.EvaManualSubmission,
                item.EvaAutomaticSubmission))
            .SingleOrDefaultAsync(cancellationToken);

        // A code naming no active principal has enabled nothing. Returning
        // Disabled rather than null keeps every caller on one branch: there is
        // no difference between "switched off" and "no such principal" that a
        // submission decision needs to act on, and inventing one would give a
        // replaced principal a route its successor controls.
        return modes ?? EvaSubmissionModes.Disabled;
    }
}
