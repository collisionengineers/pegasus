using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfStaffAccountAdministration(
    PegasusDbContext context,
    UserManager<PegasusIdentityUser> userManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictTokenManager tokenManager,
    TimeProvider timeProvider)
    : IStaffAccountQueries,
      ICreateStaffAccountStore,
      IDisableStaffAccountStore,
      IAssignStaffRolesStore,
      IReviewStaffAccessStore
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

    public async Task<CreateStaffAccountResult> CreateAsync(
        CreateStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = NormalizeUserName(request.UserName);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.EventKind != "staff_account_created"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal)
                || !Guid.TryParse(replay.AggregateId, out var replayStaffId))
            {
                throw OperationConflict();
            }

            var replayUser = await FindUserAsync(replayStaffId, cancellationToken);
            if (!string.Equals(
                    replayUser.NormalizedUserName,
                    normalizedUserName,
                    StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            var replayRoles = await GetRolesAsync(replayUser);
            var replayReviewAtUtc = await GetLastReviewAtUtcAsync(
                replayUser.Id,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(
                Summary(replayUser, replayRoles, replayReviewAtUtc),
                WasReplay: true);
        }

        if (await context.Users.AnyAsync(
                item => item.NormalizedUserName == normalizedUserName,
                cancellationToken))
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.DuplicateUserName);
        }

        var account = await CreateUserCoreAsync(
            request.Actor,
            request.UserName,
            request.TemporaryPassword,
            [StaffRole.User],
            "staff_account_created",
            request.OperationKey,
            request.Reason,
            cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(account, WasReplay: false);
    }

    public async Task<DisableStaffAccountResult> DisableAsync(
        DisableStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "staff_account_disabled"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            var replayUser = await FindUserAsync(request.StaffId, cancellationToken);
            var replayRoles = await GetRolesAsync(replayUser);
            var replayCounts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(
                Summary(
                    replayUser,
                    replayRoles,
                    await GetLastReviewAtUtcAsync(request.StaffId, cancellationToken)),
                replayCounts.Authorizations,
                replayCounts.Tokens,
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        if (user.IsEnabled
            && roles.Contains(StaffRole.Administrator)
            && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.LastAdministrator);
        }

        var before = Snapshot(user, roles);
        user.IsEnabled = false;
        var revoked = await RevokeMcpAccessAsync(user.Id, cancellationToken);
        if (before != Snapshot(user, roles))
        {
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        }

        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_disabled",
            request.OperationKey,
            before,
            Snapshot(user, roles, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.SecurityStampChanged,
            user.Id.ToString("D"),
            request.OperationKey,
            "staff_account_disabled",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            Summary(
                user,
                roles,
                await GetLastReviewAtUtcAsync(user.Id, cancellationToken)),
            revoked.Authorizations,
            revoked.Tokens,
            WasReplay: false);
    }

    public async Task<AssignStaffRolesResult> AssignAsync(
        AssignStaffRolesRequest request,
        CancellationToken cancellationToken)
    {
        var requestedRoles = request.Roles.OrderBy(role => role).ToArray();
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "staff_roles_changed"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal)
                || !RecordedRolesEqual(replay.AfterJson, requestedRoles))
            {
                throw OperationConflict();
            }

            var replayUser = await FindUserAsync(request.StaffId, cancellationToken);
            var replayCounts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(
                Summary(
                    replayUser,
                    await GetRolesAsync(replayUser),
                    await GetLastReviewAtUtcAsync(request.StaffId, cancellationToken)),
                replayCounts.Authorizations,
                replayCounts.Tokens,
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var currentRoles = await GetRolesAsync(user);
        if (user.IsEnabled
            && currentRoles.Contains(StaffRole.Administrator)
            && !requestedRoles.Contains(StaffRole.Administrator)
            && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.LastAdministrator);
        }

        var before = Snapshot(user, currentRoles);
        var rolesChanged = !currentRoles.SequenceEqual(requestedRoles);
        var revoked = (Authorizations: 0L, Tokens: 0L);
        if (rolesChanged)
        {
            revoked = await RevokeMcpAccessAsync(user.Id, cancellationToken);
            ThrowIfFailed(await userManager.RemoveFromRolesAsync(
                user,
                currentRoles.Select(RoleName)));
            ThrowIfFailed(await userManager.AddToRolesAsync(
                user,
                requestedRoles.Select(RoleName)));
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        }

        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_roles_changed",
            request.OperationKey,
            before,
            Snapshot(user, requestedRoles, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        if (rolesChanged)
        {
            AddSecurityEvent(
                SecurityEventType.SecurityStampChanged,
                user.Id.ToString("D"),
                request.OperationKey,
                "staff_roles_changed",
                now);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            Summary(
                user,
                requestedRoles,
                await GetLastReviewAtUtcAsync(user.Id, cancellationToken)),
            revoked.Authorizations,
            revoked.Tokens,
            WasReplay: false);
    }

    public async Task<ReviewStaffAccessResult> ReviewAsync(
        ReviewStaffAccessRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "access_reviewed"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            await transaction.CommitAsync(cancellationToken);
            return new(request.StaffId, replay.OccurredAtUtc, WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "access_reviewed",
            request.OperationKey,
            beforeJson: null,
            Snapshot(user, roles),
            now,
            request.Reason);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(user.Id, now, WasReplay: false);
    }

    internal async Task<StaffAccountSummary> CreateInitialAdministratorAsync(
        ActionActor actor,
        InitialAdministratorCredentials administrator,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = NormalizeUserName(administrator.UserName);
        if (await context.Users.AnyAsync(
                item => item.NormalizedUserName == normalizedUserName,
                cancellationToken))
        {
            throw new ApplicationInitializationException(
                ApplicationInitializationError.InvalidInitialAccount);
        }

        return await CreateUserCoreAsync(
            actor,
            administrator.UserName,
            administrator.TemporaryPassword,
            [StaffRole.Administrator],
            "staff_account_created",
            operationKey,
            $"Approved initial Administrator: {administrator.ManifestIdentity}",
            cancellationToken);
    }

    private async Task<StaffAccountSummary> CreateUserCoreAsync(
        ActionActor actor,
        string userName,
        string temporaryPassword,
        IReadOnlyCollection<StaffRole> roles,
        string eventKind,
        string operationKey,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = NormalizeUserName(userName);
        foreach (var role in roles)
        {
            var normalizedRoleName = RoleName(role).ToUpperInvariant();
            if (!await context.Roles.AnyAsync(
                    item => item.NormalizedName == normalizedRoleName,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "The required staff roles have not been initialized.");
            }
        }

        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            IsEnabled = true,
            MustChangePassword = true,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        ThrowIfFailed(await userManager.CreateAsync(user, temporaryPassword));
        ThrowIfFailed(await userManager.AddToRolesAsync(user, roles.Select(RoleName)));

        AddHistory(
            actor,
            user.Id,
            eventKind,
            operationKey,
            beforeJson: null,
            Snapshot(user, roles),
            timeProvider.GetUtcNow(),
            reason);
        return Summary(user, roles.OrderBy(role => role).ToArray(), lastAccessReviewAtUtc: null);
    }

    private Task<ActionHistoryEntity?> FindOperationAsync(
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.SingleOrDefaultAsync(
            item => item.AggregateType == "staff_account"
                && item.CorrelationId == operationKey,
            cancellationToken);

    private async Task<PegasusIdentityUser> FindUserAsync(
        Guid staffId,
        CancellationToken cancellationToken) =>
        await context.Users.SingleOrDefaultAsync(item => item.Id == staffId, cancellationToken)
            ?? throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.StaffAccountNotFound);

    private async Task<int> CountEnabledAdministratorsAsync(
        CancellationToken cancellationToken)
    {
        var administratorRoleName = StaffRoleNames.Administrator.ToUpperInvariant();
        return await (
            from user in context.Users
            join userRole in context.UserRoles on user.Id equals userRole.UserId
            join role in context.Roles on userRole.RoleId equals role.Id
            where user.IsEnabled && role.NormalizedName == administratorRoleName
            select user.Id)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private async Task<StaffRole[]> GetRolesAsync(PegasusIdentityUser user)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        return roleNames.Select(ParseRole).OrderBy(role => role).ToArray();
    }

    private Task<DateTimeOffset?> GetLastReviewAtUtcAsync(
        Guid staffId,
        CancellationToken cancellationToken) =>
        context.ActionHistory
            .Where(item => item.AggregateType == "staff_account"
                && item.AggregateId == staffId.ToString("D")
                && item.EventKind == "access_reviewed")
            .Select(item => (DateTimeOffset?)item.OccurredAtUtc)
            .MaxAsync(cancellationToken);

    private async Task<(long Authorizations, long Tokens)> RevokeMcpAccessAsync(
        Guid staffId,
        CancellationToken cancellationToken)
    {
        var subject = staffId.ToString("D");
        var tokens = await tokenManager.RevokeBySubjectAsync(subject, cancellationToken);
        var authorizations = await authorizationManager.RevokeBySubjectAsync(
            subject,
            cancellationToken);
        return (authorizations, tokens);
    }

    private void AddHistory(
        ActionActor actor,
        Guid staffId,
        string eventKind,
        string operationKey,
        string? beforeJson,
        string afterJson,
        DateTimeOffset occurredAtUtc,
        string? reason = null)
    {
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "staff_account",
            AggregateId = staffId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                actor.Roles.OrderBy(role => role).Select(RoleName)),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        });
    }

    private void AddSecurityEvent(
        SecurityEventType type,
        string subjectId,
        string correlationId,
        string reasonCode,
        DateTimeOffset occurredAtUtc)
    {
        context.SecurityEvents.Add(new SecurityEventEntity
        {
            Id = Guid.NewGuid(),
            Type = type.ToString(),
            Outcome = SecurityEventOutcome.Succeeded.ToString(),
            SubjectId = subjectId,
            OccurredAtUtc = occurredAtUtc,
            CorrelationId = correlationId,
            ReasonCode = reasonCode
        });
    }

    private static StaffAccountSummary Summary(
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

    private static string Snapshot(
        PegasusIdentityUser user,
        IReadOnlyCollection<StaffRole> roles,
        long revokedAuthorizations = 0,
        long revokedTokens = 0) =>
        JsonSerializer.Serialize(new
        {
            user.Id,
            user.UserName,
            user.IsEnabled,
            user.MustChangePassword,
            Roles = roles.OrderBy(role => role).Select(RoleName),
            RevokedAuthorizations = revokedAuthorizations,
            RevokedTokens = revokedTokens
        });

    private static bool RecordedRolesEqual(
        string? afterJson,
        IReadOnlyCollection<StaffRole> requestedRoles)
    {
        if (afterJson is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(afterJson);
        if (!document.RootElement.TryGetProperty("Roles", out var roleElement)
            || roleElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var recordedRoles = roleElement.EnumerateArray()
            .Select(item => ParseRole(item.GetString() ?? string.Empty))
            .OrderBy(role => role);
        return recordedRoles.SequenceEqual(requestedRoles.OrderBy(role => role));
    }

    private static (long Authorizations, long Tokens) ParseRevocationCounts(
        string? afterJson)
    {
        if (afterJson is null)
        {
            return (0, 0);
        }

        using var document = JsonDocument.Parse(afterJson);
        var authorizations = document.RootElement.TryGetProperty(
                "RevokedAuthorizations",
                out var authorizationElement)
            && authorizationElement.TryGetInt64(out var authorizationCount)
                ? authorizationCount
                : 0;
        var tokens = document.RootElement.TryGetProperty(
                "RevokedTokens",
                out var tokenElement)
            && tokenElement.TryGetInt64(out var tokenCount)
                ? tokenCount
                : 0;
        return (authorizations, tokens);
    }

    private string NormalizeUserName(string userName) =>
        userManager.NormalizeName(userName)
            ?? throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.InvalidAccount);

    private static StaffAccountAdministrationException OperationConflict() =>
        new(StaffAccountAdministrationError.OperationConflict);

    private static StaffRole ParseRole(string roleName) => roleName switch
    {
        StaffRoleNames.Administrator => StaffRole.Administrator,
        StaffRoleNames.Engineer => StaffRole.Engineer,
        StaffRoleNames.User => StaffRole.User,
        _ => throw new InvalidOperationException(
            "A staff account has an unrecognized role.")
    };

    private static string RoleName(StaffRole role) => role switch
    {
        StaffRole.Administrator => StaffRoleNames.Administrator,
        StaffRole.Engineer => StaffRoleNames.Engineer,
        StaffRole.User => StaffRoleNames.User,
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.InvalidAccount);
        }
    }
}
