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
public sealed class EfStaffAccountQueries(PegasusDbContext context)
    : IStaffAccountQueries,
      ICaseEngineerChoices
{
    public async Task<IReadOnlyList<CaseEngineerChoice>> GetAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var engineerRoleName = StaffRoleNames.Engineer.ToUpperInvariant();
        return await context.Users.AsNoTracking()
            .Where(user => user.IsEnabled
                && user.UserName != null
                && context.UserRoles
                .Join(context.Roles, userRole => userRole.RoleId, role => role.Id,
                    (userRole, role) => new { userRole.UserId, role.NormalizedName })
                .Any(role => role.UserId == user.Id && role.NormalizedName == engineerRoleName))
            .OrderBy(user => user.UserName)
            .ThenBy(user => user.Id)
            .Select(user => new CaseEngineerChoice(user.Id, user.UserName!))
            .ToListAsync(cancellationToken);
    }

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
            // Nullable on purpose: the lookup below reads a missing account
            // with GetValueOrDefault, and a non-nullable DateTimeOffset would
            // hand back 0001-01-01 rather than nothing — which reads as "this
            // account was reviewed" and makes Core's ReviewIsOutstanding
            // permanently false (PLAT-027).
            .ToDictionaryAsync(
                item => item.AggregateId,
                item => (DateTimeOffset?)item.OccurredAtUtc,
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

    public async Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
        CancellationToken cancellationToken)
    {
        var engineerRoleName = StaffRoleNames.Engineer.ToUpperInvariant();
        var candidates = await (
            from user in context.Users.AsNoTracking()
            join userRole in context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where user.IsSignOffEngineer && role.NormalizedName == engineerRoleName
            orderby user.SignOffPrintedName, user.Id
            select user)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(user => SignOffEngineerEligibility.IsEligible(
                user.IsEnabled,
                [StaffRole.Engineer],
                user.IsSignOffEngineer,
                user.SignOffSignature))
            .Select(Profile)
            .ToArray();
    }

    public async Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
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
        var parsedRoles = roles.Select(ParseRole).ToArray();
        return SignOffEngineerEligibility.IsEligible(
            user.IsEnabled,
            parsedRoles,
            user.IsSignOffEngineer,
            user.SignOffSignature)
            ? Profile(user)
            : null;
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
            lastAccessReviewAtUtc)
        {
            SignOff = new(
                user.IsSignOffEngineer,
                user.SignOffPrintedName,
                user.SignOffQualifications,
                user.SignOffSignature is { Length: > 0 },
                user.IsDefaultSignOffEngineer)
        };

    private static SignOffEngineerProfile Profile(PegasusIdentityUser user) =>
        new(
            user.Id,
            user.SignOffPrintedName ?? throw new InvalidOperationException(
                "An eligible sign-off Engineer has no printed name."),
            user.SignOffQualifications,
            user.SignOffSignature?.ToArray() ?? throw new InvalidOperationException(
                "An eligible sign-off Engineer has no signature."),
            SignOffSignaturePolicy.MediaType,
            user.IsDefaultSignOffEngineer);

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
