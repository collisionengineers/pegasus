using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The read side of staff account lookup: username, enabled state, roles and
/// last access review, straight off the Identity tables via EF. Unlike
/// <see cref="EfStaffAccountAdministration"/> (which owns the mutations and
/// needs <c>UserManager</c> for password and security-stamp handling), this
/// class depends only on <see cref="PegasusDbContext"/> — so a host that never
/// composes ASP.NET Identity (the Worker, and any Infrastructure-only test
/// host) can still resolve <see cref="IStaffAccountQueries"/> to look up a
/// staff display name.
/// </summary>
public sealed class EfStaffAccountQueries(PegasusDbContext context) : IStaffAccountQueries
{
    public async Task<StaffAccountQuerySlice> ListAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .OrderBy(item => item.UserName)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);
        var hasMoreAccounts = users.Count > limit;
        if (hasMoreAccounts)
        {
            users.RemoveAt(users.Count - 1);
        }

        if (users.Count == 0)
        {
            return new([], hasMoreAccounts);
        }

        var userIds = users.Select(user => user.Id).ToArray();
        var roleRows = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);
        var aggregateIds = userIds.Select(id => id.ToString("D")).ToArray();
        var reviews = await context.ActionHistory
            .AsNoTracking()
            .Where(item => item.AggregateType == "staff_account"
                && item.EventKind == "access_reviewed"
                && aggregateIds.Contains(item.AggregateId))
            .GroupBy(item => item.AggregateId)
            .Select(group => new
            {
                AggregateId = group.Key,
                OccurredAtUtc = group.Max(item => item.OccurredAtUtc)
            })
            .ToDictionaryAsync(
                item => item.AggregateId,
                item => item.OccurredAtUtc,
                cancellationToken);
        var rolesByUser = roleRows.ToLookup(
            item => item.UserId,
            item => ParseRole(item.RoleName));

        return new(
            users.Select(user => Summary(
                    user,
                    rolesByUser[user.Id].OrderBy(role => role).ToArray(),
                    reviews.GetValueOrDefault(user.Id.ToString("D"))))
                .ToArray(),
            hasMoreAccounts);
    }

    public async Task<StaffAccountSummary?> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == staffId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == staffId
            select role.Name!)
            .ToListAsync(cancellationToken);
        var lastReviewAtUtc = await context.ActionHistory
            .AsNoTracking()
            .Where(item => item.AggregateType == "staff_account"
                && item.AggregateId == staffId.ToString("D")
                && item.EventKind == "access_reviewed")
            .Select(item => (DateTimeOffset?)item.OccurredAtUtc)
            .MaxAsync(cancellationToken);
        return Summary(
            user,
            roles.Select(ParseRole).OrderBy(role => role).ToArray(),
            lastReviewAtUtc);
    }

    /// <summary>Shared with <see cref="EfStaffAccountAdministration"/> so the mapping lives once.</summary>
    internal static StaffAccountSummary Summary(
        PegasusIdentityUser user,
        IReadOnlyCollection<StaffRole> roles,
        DateTimeOffset? lastAccessReviewAtUtc) =>
        new(
            user.Id,
            user.UserName ?? throw new InvalidOperationException(
                "A staff account has no username."),
            user.IsEnabled,
            user.MustChangePassword,
            roles.OrderBy(role => role).ToArray(),
            lastAccessReviewAtUtc);

    /// <summary>Shared with <see cref="EfStaffAccountAdministration"/> so the mapping lives once.</summary>
    internal static StaffRole ParseRole(string roleName) => roleName switch
    {
        StaffRoleNames.Administrator => StaffRole.Administrator,
        StaffRoleNames.Engineer => StaffRole.Engineer,
        StaffRoleNames.User => StaffRole.User,
        _ => throw new InvalidOperationException(
            "A staff account has an unrecognized role.")
    };
}
