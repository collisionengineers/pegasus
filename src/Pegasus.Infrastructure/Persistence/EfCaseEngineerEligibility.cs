using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The Engineer-eligibility read the Case stores make inside their own transaction
/// (Start, Assign, Return to Engineer and Create audit).
/// </summary>
internal static class EfCaseEngineerEligibility
{
    internal static async Task<CaseEngineerEligibility> ReadAsync(
        PegasusDbContext context,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == staffId)
            .Select(user => new CaseEngineerEligibility(true, user.IsEnabled))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new CaseEngineerEligibility(false, false);
    }
}
