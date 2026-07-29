using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfStaffAccountAdministration(
    PegasusDbContext context,
    UserManager<PegasusIdentityUser> userManager,
    TimeProvider timeProvider)
    : IStaffAccountAdministration
{
    public async Task<IReadOnlyList<StaffAccountSummary>> ListAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ReviewStaffAccess);

        var users = await context.Users
            .AsNoTracking()
            .OrderBy(item => item.UserName)
            .ToListAsync(cancellationToken);
        var roleRows = await (
            from userRole in context.UserRoles.AsNoTracking()
            join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            select new { userRole.UserId, RoleName = role.Name! })
            .ToListAsync(cancellationToken);
        var reviews = await context.ActionHistory
            .AsNoTracking()
            .Where(item => item.AggregateType == "staff_account" && item.EventKind == "access_reviewed")
            .GroupBy(item => item.AggregateId)
            .Select(group => new { AggregateId = group.Key, OccurredAtUtc = group.Max(item => item.OccurredAtUtc) })
            .ToDictionaryAsync(item => item.AggregateId, item => item.OccurredAtUtc, cancellationToken);
        var rolesByUser = roleRows.ToLookup(item => item.UserId, item => ParseRole(item.RoleName));

        return users.Select(user => new StaffAccountSummary(
                user.Id,
                user.UserName ?? throw new InvalidOperationException("A staff account has no username."),
                user.IsEnabled,
                user.MustChangePassword,
                rolesByUser[user.Id].OrderBy(role => role).ToArray(),
                reviews.GetValueOrDefault(user.Id.ToString("D"))))
            .ToArray();
    }

    public async Task<StaffAccountSummary> CreateAsync(
        ActionActor actor,
        string userName,
        string temporaryPassword,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        ValidateUserName(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPassword);
        ValidateOperationKey(operationKey);
        var normalizedUserName = userManager.NormalizeName(userName.Trim())
            ?? throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.InvalidAccount);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.EventKind != "staff_account_created"
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
            await transaction.CommitAsync(cancellationToken);
            return new(
                replayUser.Id,
                replayUser.UserName!,
                replayUser.IsEnabled,
                replayUser.MustChangePassword,
                replayRoles,
                null);
        }
        if (await context.Users.AnyAsync(item => item.NormalizedUserName == normalizedUserName, cancellationToken))
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.DuplicateUserName);
        }

        var userRoleName = StaffRoleNames.User.ToUpperInvariant();
        var userRole = await context.Roles.SingleOrDefaultAsync(
            item => item.NormalizedName == userRoleName,
            cancellationToken)
            ?? throw new InvalidOperationException("The required staff roles have not been initialized.");
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName.Trim(),
            NormalizedUserName = normalizedUserName,
            IsEnabled = true,
            MustChangePassword = true,
            LockoutEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        ThrowIfFailed(await userManager.CreateAsync(user, temporaryPassword));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, userRole.Name!));

        var now = timeProvider.GetUtcNow();
        AddHistory(actor, user.Id, "staff_account_created", operationKey, null, Snapshot(user, [StaffRole.User]), now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(user.Id, user.UserName, true, true, [StaffRole.User], null);
    }

    public async Task SetEnabledAsync(
        ActionActor actor,
        Guid staffId,
        bool enabled,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        ValidateMutation(staffId, reason, operationKey);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (await IsReplayAsync(
                operationKey,
                staffId,
                enabled ? "staff_account_enabled" : "staff_account_disabled",
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        var user = await FindUserAsync(staffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        if (user.IsEnabled != enabled)
        {
            if (!enabled
                && roles.Contains(StaffRole.Administrator)
                && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
            {
                throw new StaffAccountAdministrationException(
                    StaffAccountAdministrationError.LastAdministrator);
            }

            var before = Snapshot(user, roles);
            user.IsEnabled = enabled;
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
            AddHistory(actor, staffId, enabled ? "staff_account_enabled" : "staff_account_disabled", operationKey, before, Snapshot(user, roles), timeProvider.GetUtcNow(), reason);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var snapshot = Snapshot(user, roles);
            AddHistory(actor, staffId, enabled ? "staff_account_enabled" : "staff_account_disabled", operationKey, snapshot, snapshot, timeProvider.GetUtcNow(), reason);
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetRolesAsync(
        ActionActor actor,
        Guid staffId,
        IReadOnlyCollection<StaffRole> roles,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.AssignStaffRoles);
        ValidateMutation(staffId, reason, operationKey);
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0 || roles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentException("An enabled staff account requires recognized current roles.", nameof(roles));
        }

        var requestedRoles = roles.Distinct().OrderBy(role => role).ToArray();
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (await IsRoleReplayAsync(
                operationKey,
                staffId,
                requestedRoles,
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        var user = await FindUserAsync(staffId, cancellationToken);
        var currentRoles = await GetRolesAsync(user);
        if (user.IsEnabled
            && currentRoles.Contains(StaffRole.Administrator)
            && !requestedRoles.Contains(StaffRole.Administrator)
            && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.LastAdministrator);
        }
        if (!currentRoles.OrderBy(role => role).SequenceEqual(requestedRoles))
        {
            var before = Snapshot(user, currentRoles);
            ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, currentRoles.Select(RoleName)));
            ThrowIfFailed(await userManager.AddToRolesAsync(user, requestedRoles.Select(RoleName)));
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
            AddHistory(actor, staffId, "staff_roles_changed", operationKey, before, Snapshot(user, requestedRoles), timeProvider.GetUtcNow(), reason);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var snapshot = Snapshot(user, currentRoles);
            AddHistory(actor, staffId, "staff_roles_changed", operationKey, snapshot, snapshot, timeProvider.GetUtcNow(), reason);
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReviewAccessAsync(
        ActionActor actor,
        Guid staffId,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ReviewStaffAccess);
        ValidateMutation(staffId, reason, operationKey);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (await IsReplayAsync(
                operationKey,
                staffId,
                "access_reviewed",
                cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        var user = await FindUserAsync(staffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        AddHistory(actor, staffId, "access_reviewed", operationKey, null, Snapshot(user, roles), timeProvider.GetUtcNow(), reason);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task<ActionHistoryEntity?> FindOperationAsync(
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.SingleOrDefaultAsync(
            item => item.AggregateType == "staff_account"
                && item.CorrelationId == operationKey,
            cancellationToken);

    private async Task<bool> IsReplayAsync(
        string operationKey,
        Guid staffId,
        string eventKind,
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(operationKey, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.AggregateType == "staff_account"
            && existing.AggregateId == staffId.ToString("D")
            && existing.EventKind == eventKind)
        {
            return true;
        }

        throw OperationConflict();
    }

    private async Task<bool> IsRoleReplayAsync(
        string operationKey,
        Guid staffId,
        IReadOnlyCollection<StaffRole> requestedRoles,
        CancellationToken cancellationToken)
    {
        var existing = await FindOperationAsync(operationKey, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        if (existing.AggregateId != staffId.ToString("D")
            || existing.EventKind != "staff_roles_changed"
            || existing.AfterJson is null)
        {
            throw OperationConflict();
        }

        using var document = JsonDocument.Parse(existing.AfterJson);
        if (!document.RootElement.TryGetProperty("Roles", out var roleElement)
            || roleElement.ValueKind != JsonValueKind.Array)
        {
            throw OperationConflict();
        }

        var recordedRoles = roleElement.EnumerateArray()
            .Select(item => ParseRole(item.GetString() ?? string.Empty))
            .OrderBy(role => role);
        if (!recordedRoles.SequenceEqual(requestedRoles.OrderBy(role => role)))
        {
            throw OperationConflict();
        }

        return true;
    }

    private static StaffAccountAdministrationException OperationConflict() =>
        new(StaffAccountAdministrationError.OperationConflict);

    private async Task<PegasusIdentityUser> FindUserAsync(Guid staffId, CancellationToken cancellationToken)
    {
        return await context.Users.SingleOrDefaultAsync(item => item.Id == staffId, cancellationToken)
            ?? throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.StaffAccountNotFound);
    }

    private async Task<int> CountEnabledAdministratorsAsync(CancellationToken cancellationToken)
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
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role).Select(RoleName)),
            OccurredAtUtc = occurredAtUtc,
            Outcome = "succeeded",
            CorrelationId = operationKey,
            Reason = reason,
            BeforeJson = beforeJson,
            AfterJson = afterJson
        });
    }

    private static string Snapshot(PegasusIdentityUser user, IReadOnlyCollection<StaffRole> roles) =>
        JsonSerializer.Serialize(new
        {
            user.Id,
            user.UserName,
            user.IsEnabled,
            user.MustChangePassword,
            Roles = roles.OrderBy(role => role).Select(RoleName)
        });

    private static StaffRole ParseRole(string roleName) => roleName switch
    {
        StaffRoleNames.Administrator => StaffRole.Administrator,
        StaffRoleNames.Engineer => StaffRole.Engineer,
        StaffRoleNames.User => StaffRole.User,
        _ => throw new InvalidOperationException("A staff account has an unrecognized role.")
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

    private static void ValidateUserName(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        if (userName.Trim().Length > 256)
        {
            throw new ArgumentException("The username cannot exceed 256 characters.", nameof(userName));
        }
    }

    private static void ValidateMutation(Guid staffId, string reason, string operationKey)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("A staff account identifier is required.", nameof(staffId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Length > 1000)
        {
            throw new ArgumentException("The reason cannot exceed 1000 characters.", nameof(reason));
        }
        ValidateOperationKey(operationKey);
    }

    private static void ValidateOperationKey(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Length > 100)
        {
            throw new ArgumentException("The operation key cannot exceed 100 characters.", nameof(operationKey));
        }
    }
}

