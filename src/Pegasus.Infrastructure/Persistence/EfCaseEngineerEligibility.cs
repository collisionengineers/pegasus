using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseEngineerEligibility(
    IDbContextFactory<PegasusDbContext> contextFactory) : ICaseEngineerEligibility
{
    private static readonly string EngineerRoleName =
        StaffRoleNames.Engineer.ToUpperInvariant();

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
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Id == staffId)
            .Select(user => new CaseEngineerEligibility(
                true,
                user.IsEnabled,
                context.UserRoles
                    .Join(
                        context.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (userRole, role) => new { userRole.UserId, role.NormalizedName })
                    .Any(item => item.UserId == user.Id && item.NormalizedName == EngineerRoleName)))
            .SingleOrDefaultAsync(cancellationToken)
            ?? new CaseEngineerEligibility(false, false, false);
    }
}
