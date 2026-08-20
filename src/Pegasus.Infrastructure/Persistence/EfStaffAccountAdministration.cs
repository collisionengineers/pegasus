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
    : ICreateStaffAccountStore,
      IDisableStaffAccountStore,
      IAssignStaffRolesStore,
      IReviewStaffAccessStore
{
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
                EfStaffAccountQueries.Summary(replayUser, replayRoles, replayReviewAtUtc),
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
                EfStaffAccountQueries.Summary(
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
        var revoked = (Authorizations: 0L, Tokens: 0L);
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
            EfStaffAccountQueries.Summary(
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
                EfStaffAccountQueries.Summary(
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
            EfStaffAccountQueries.Summary(
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
        return EfStaffAccountQueries.Summary(user, roles.OrderBy(role => role).ToArray(), lastAccessReviewAtUtc: null);
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
        return roleNames.Select(EfStaffAccountQueries.ParseRole).OrderBy(role => role).ToArray();
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
            .Select(item => EfStaffAccountQueries.ParseRole(item.GetString() ?? string.Empty))
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
