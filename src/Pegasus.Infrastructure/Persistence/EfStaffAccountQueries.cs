using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The read side of staff account lookup: username, enabled state, roles and
/// current account state, straight off the Identity tables via EF. Unlike
/// <see cref="EfStaffAccountAdministration"/> (which owns the mutations and
/// needs <c>UserManager</c> for password and security-stamp handling), this
/// class depends only on the context factory — so a host that never composes
/// ASP.NET Identity (the Worker, and any Infrastructure-only test host) can
/// still resolve <see cref="IStaffAccountQueries"/> to look up a staff display
/// name. Each read uses its own context, never the request's scoped one, so a
/// page may run it beside other reads.
/// </summary>
public sealed class EfStaffAccountQueries(IDbContextFactory<PegasusDbContext> contextFactory)
    : IStaffAccountQueries,
      ICaseEngineerChoices
{
    public async Task<IReadOnlyList<CaseEngineerChoice>> GetAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Users.AsNoTracking()
            .Where(user => user.IsEnabled
                && user.UserName != null)
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
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var users = await context.Users
            .AsNoTracking()
            .OrderBy(item => item.UserName)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(limit + 1)
            .Select(ToRow)
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
        var rolesByUser = roleRows.ToDictionary(
            item => item.UserId,
            item => ParseRole(item.RoleName));

        return new(
            users.Select(user => Summary(
                    user,
                    rolesByUser.TryGetValue(user.Id, out var role)
                        ? role
                        : throw new InvalidOperationException("A staff account has no role.")))
                .ToArray(),
            hasMoreAccounts);
    }

    public async Task<StaffAccountSummary?> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var user = await context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == staffId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roleNames = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.UserId == staffId
            select role.Name!)
            .ToListAsync(cancellationToken);
        return Summary(user, ParseSingleRole(roleNames));
    }

    public async Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> staffIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(staffIds);
        if (staffIds.Count == 0)
        {
            return [];
        }

        var ids = staffIds.Distinct().ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // One row per user with its role names collected, so an account with
        // no role or two roles fails the same exactly-one-role invariant as
        // GetAsync instead of vanishing or appearing twice. The row is the
        // columns a summary shows, never the password hash or the sign-off
        // signature bytes. It repeats ToRow because a correlated role list
        // cannot be composed into that expression, and its signature flag is
        // DATALENGTH for the same reason ToRow's is.
        var accounts = await (
            from user in context.Users.AsNoTracking()
            where ids.Contains(user.Id)
            select new
            {
                Row = new StaffAccountRow(
                    user.Id,
                    user.UserName,
                    user.IsEnabled,
                    user.MustChangePassword,
                    user.Version,
                    user.WorkCentreLastSeenUtc,
                    user.IsSignOffEngineer,
                    user.SignOffPrintedName,
                    user.SignOffQualifications,
                    EF.Functions.DataLength(user.SignOffSignature) > 0,
                    user.IsDefaultSignOffEngineer),
                RoleNames = (
                    from userRole in context.UserRoles
                    join role in context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name!).ToList()
            })
            .ToListAsync(cancellationToken);

        return accounts
            .Select(account => Summary(account.Row, ParseSingleRole(account.RoleNames)))
            .ToArray();
    }

    public async Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ListSignOffEngineersAsync(context, cancellationToken);
    }

    /// <summary>
    /// The eligible sign-off Engineers read on a caller's own context, so a
    /// store that already holds one (and perhaps its transaction) reads them
    /// there.
    /// </summary>
    internal static async Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        var candidates = await (
            from user in context.Users.AsNoTracking()
            where user.IsEnabled
                && user.IsSignOffEngineer
                && user.SignOffSignature != null
            orderby user.SignOffPrintedName, user.Id
            select user)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(user => SignOffEngineerEligibility.IsEligible(
                user.IsEnabled,
                user.IsSignOffEngineer,
                user.SignOffSignature))
            .Select(Profile)
            .ToArray();
    }

    /// <summary>
    /// What a <see cref="StaffAccountSummary"/> shows of an account, without
    /// the password hash, the security stamps or the sign-off signature bytes
    /// (a signature can be up to 1 MiB). Whether there is a signature is
    /// worked out in SQL from the stored length (<see cref="ToRow"/>).
    /// </summary>
    private sealed record StaffAccountRow(
        Guid Id,
        string? UserName,
        bool IsEnabled,
        bool MustChangePassword,
        long Version,
        DateTimeOffset? WorkCentreLastSeenUtc,
        bool IsSignOffEngineer,
        string? SignOffPrintedName,
        string? SignOffQualifications,
        bool HasSignature,
        bool IsDefaultSignOffEngineer);

    /// <summary>
    /// The row as SQL reads it: only the summary columns, with "has a
    /// signature" as <c>DATALENGTH</c> of the stored bytes so the bytes stay
    /// in the database. EF does not translate <c>byte[].Length</c>; written
    /// that way it selects the whole column and measures it in memory.
    /// <c>EF.Functions</c> has no in-memory form, so <see cref="RowOf"/> builds
    /// the same row from an entity that is already loaded.
    /// </summary>
    private static readonly Expression<Func<PegasusIdentityUser, StaffAccountRow>> ToRow =
        user => new StaffAccountRow(
            user.Id,
            user.UserName,
            user.IsEnabled,
            user.MustChangePassword,
            user.Version,
            user.WorkCentreLastSeenUtc,
            user.IsSignOffEngineer,
            user.SignOffPrintedName,
            user.SignOffQualifications,
            EF.Functions.DataLength(user.SignOffSignature) > 0,
            user.IsDefaultSignOffEngineer);

    private static StaffAccountRow RowOf(PegasusIdentityUser user) =>
        new(
            user.Id,
            user.UserName,
            user.IsEnabled,
            user.MustChangePassword,
            user.Version,
            user.WorkCentreLastSeenUtc,
            user.IsSignOffEngineer,
            user.SignOffPrintedName,
            user.SignOffQualifications,
            user.SignOffSignature is { Length: > 0 },
            user.IsDefaultSignOffEngineer);

    /// <summary>Shared with <see cref="EfStaffAccountAdministration"/> so the mapping lives once.</summary>
    internal static StaffAccountSummary Summary(
        PegasusIdentityUser user,
        StaffRole role) =>
        Summary(RowOf(user), role);

    private static StaffAccountSummary Summary(StaffAccountRow row, StaffRole role) =>
        new(
            row.Id,
            row.UserName ?? throw new InvalidOperationException(
                "A staff account has no username."),
            row.IsEnabled,
            row.MustChangePassword,
            role)
        {
            Version = row.Version,
            WorkCentreLastSeenUtc = row.WorkCentreLastSeenUtc,
            SignOff = new(
                row.IsSignOffEngineer,
                row.SignOffPrintedName,
                row.SignOffQualifications,
                row.HasSignature,
                row.IsDefaultSignOffEngineer)
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

    private static StaffRole ParseSingleRole(List<string> roleNames) =>
        roleNames.Count == 1
            ? ParseRole(roleNames.Single())
            : throw new InvalidOperationException(
                "A staff account must have exactly one role.");
}
