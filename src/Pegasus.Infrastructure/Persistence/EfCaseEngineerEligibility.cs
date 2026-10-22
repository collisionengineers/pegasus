using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseEngineerEligibility(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseEngineerEligibility
{
    private readonly IDbContextFactory<PegasusDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<CaseEngineerEligibility> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("A staff identifier is required.", nameof(staffId));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadAsync(context, staffId, cancellationToken);
    }

    /// <summary>The same read inside a caller's transaction.</summary>
    internal static async Task<CaseEngineerEligibility> ReadAsync(
        PegasusDbContext context,
        Guid staffId,
        CancellationToken cancellationToken) =>
        await context.Users
            .AsNoTracking()
            .Where(user => user.Id == staffId)
            .Select(user => new CaseEngineerEligibility(true, user.IsEnabled))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new CaseEngineerEligibility(false, false);
}
