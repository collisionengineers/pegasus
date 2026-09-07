using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfStaffAccountAdministration(
    PegasusDbContext context,
    UserManager<PegasusIdentityUser> userManager,
    TimeProvider timeProvider)
    : ICreateStaffAccountStore,
      IDisableStaffAccountStore,
      IAssignStaffRolesStore,
      IEnableStaffAccountStore,
      IForceStaffLogoutStore,
      IResetStaffPasswordStore,
      IDeleteStaffAccountStore,
      IUpdateStaffAccountSignOffStore
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
            await transaction.CommitAsync(cancellationToken);
            return new(
                EfStaffAccountQueries.Summary(replayUser, replayRoles),
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
        try
        {
            return await DisableCoreAsync(request, cancellationToken);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            throw OperationConflict();
        }
    }

    private async Task<DisableStaffAccountResult> DisableCoreAsync(
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
                    replayRoles),
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
        revoked = await RevokeAuthorizationsAndTokensAsync(
            user.Id,
            scrubTokenMaterial: false,
            cancellationToken);

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
                roles),
            revoked.Authorizations,
            revoked.Tokens,
            WasReplay: false);
    }

    public async Task<AssignStaffRolesResult> AssignAsync(
        AssignStaffRolesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await AssignCoreAsync(request, cancellationToken);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            throw OperationConflict();
        }
    }

    private async Task<AssignStaffRolesResult> AssignCoreAsync(
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
                    await GetRolesAsync(replayUser)),
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
            revoked = await RevokeAuthorizationsAndTokensAsync(
                user.Id,
                scrubTokenMaterial: false,
                cancellationToken);
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
                requestedRoles),
            revoked.Authorizations,
            revoked.Tokens,
            WasReplay: false);
    }

    public async Task<EnableStaffAccountResult> EnableAsync(
        EnableStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "staff_account_enabled"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal))
            {
                throw OperationConflict();
            }

            await transaction.CommitAsync(cancellationToken);
            var replayUser = await FindUserAsync(request.StaffId, cancellationToken);
            return new(
                EfStaffAccountQueries.Summary(replayUser, await GetRolesAsync(replayUser)),
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        if (roles.Length == 0)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.InvalidAccount);
        }

        var before = Snapshot(user, roles);
        user.IsEnabled = true;
        if (before != Snapshot(user, roles))
        {
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
            _ = await RevokeAuthorizationsAndTokensAsync(
                user.Id,
                scrubTokenMaterial: false,
                cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_enabled",
            request.OperationKey,
            before,
            Snapshot(user, roles),
            now,
            request.Reason);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(EfStaffAccountQueries.Summary(user, roles), WasReplay: false);
    }

    public async Task<ForceStaffLogoutResult> ForceLogoutAsync(
        ForceStaffLogoutRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, request.StaffId, "staff_logout_forced", request.Reason);
            var counts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(request.StaffId, counts.Authorizations, counts.Tokens, WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        var before = Snapshot(user, roles);
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        var revoked = await RevokeAuthorizationsAndTokensAsync(
            user.Id,
            scrubTokenMaterial: false,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_logout_forced",
            request.OperationKey,
            before,
            Snapshot(user, roles, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.SecurityStampChanged,
            user.Id.ToString("D"),
            request.OperationKey,
            "staff_logout_forced",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(user.Id, revoked.Authorizations, revoked.Tokens, WasReplay: false);
    }

    public async Task<ResetStaffPasswordResult> ResetPasswordAsync(
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        if (await FindOperationAsync(request.OperationKey, cancellationToken) is not null)
        {
            // The generated value is deliberately never persisted, so it cannot be
            // revealed again by replaying an administrator request.
            throw OperationConflict();
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        if (!user.IsEnabled)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.DisabledAccount);
        }

        var temporaryPassword = GenerateTemporaryPassword();
        var roles = await GetRolesAsync(user);
        var before = Snapshot(user, roles);
        user.PasswordHash = userManager.PasswordHasher.HashPassword(user, temporaryPassword);
        user.MustChangePassword = true;
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        var revoked = await RevokeAuthorizationsAndTokensAsync(
            user.Id,
            scrubTokenMaterial: false,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_password_reset",
            request.OperationKey,
            before,
            Snapshot(user, roles, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.PasswordChanged,
            user.Id.ToString("D"),
            request.OperationKey,
            "staff_password_reset",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            user.Id,
            temporaryPassword,
            revoked.Authorizations,
            revoked.Tokens,
            wasReplay: false);
    }

    public async Task<DeleteStaffAccountResult> DeleteAsync(
        DeleteStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            EnsureReplay(replay, request.StaffId, "staff_account_deleted", request.Reason);
            var counts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(
                request.StaffId,
                counts.Authorizations,
                counts.Tokens,
                CredentialsCleared: true,
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
        if (roles.Length > 0)
        {
            ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, roles.Select(RoleName)));
        }

        user.IsEnabled = false;
        user.MustChangePassword = true;
        user.PasswordHash = null;
        user.IsSignOffEngineer = false;
        user.SignOffPrintedName = null;
        user.SignOffQualifications = null;
        user.SignOffSignature = null;
        user.SignOffSignatureDigest = null;
        user.IsDefaultSignOffEngineer = false;
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        var revoked = await RevokeAuthorizationsAndTokensAsync(
            user.Id,
            scrubTokenMaterial: true,
            cancellationToken);
        ClearExternalCredentialsAndSessions(user.Id);
        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_deleted",
            request.OperationKey,
            before,
            Snapshot(user, [], revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.SecurityStampChanged,
            user.Id.ToString("D"),
            request.OperationKey,
            "staff_account_deleted",
            now);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            user.Id,
            revoked.Authorizations,
            revoked.Tokens,
            CredentialsCleared: true,
            WasReplay: false);
    }

    public async Task<UpdateStaffAccountSignOffResult> UpdateAsync(
        UpdateStaffAccountSignOffRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "staff_account_sign_off_updated"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal)
                || !RecordedSignOffEqual(replay.AfterJson, request))
            {
                throw OperationConflict();
            }

            var replayUser = await FindUserAsync(request.StaffId, cancellationToken);
            var replayRoles = await GetRolesAsync(replayUser);
            await transaction.CommitAsync(cancellationToken);
            return new(
                EfStaffAccountQueries.Summary(
                    replayUser,
                    replayRoles),
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var roles = await GetRolesAsync(user);
        if (!roles.Contains(StaffRole.Engineer))
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.SignOffEngineerRequiresEngineerRole);
        }

        var previousDefault = await context.Users.SingleOrDefaultAsync(
            item => item.IsDefaultSignOffEngineer,
            cancellationToken);
        var previousDefaultId = previousDefault?.Id;
        var before = SignOffSnapshot(user, previousDefaultId);

        user.IsSignOffEngineer = request.IsSignOffEngineer;
        user.SignOffPrintedName = request.PrintedName;
        user.SignOffQualifications = request.Qualifications;
        if (request.Signature is not null)
        {
            user.SignOffSignature = request.Signature;
            user.SignOffSignatureDigest = SignatureDigest(request.Signature);
        }

        if (request.IsDefault
            && !SignOffEngineerEligibility.IsEligible(
                user.IsEnabled,
                roles,
                user.IsSignOffEngineer,
                user.SignOffSignature))
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.IneligibleSignOffEngineer);
        }

        if (request.IsDefault && previousDefault is not null && previousDefault.Id != user.Id)
        {
            previousDefault.IsDefaultSignOffEngineer = false;
            await context.SaveChangesAsync(cancellationToken);
        }

        user.IsDefaultSignOffEngineer = request.IsDefault;
        var newDefaultId = request.IsDefault
            ? user.Id
            : previousDefault is { Id: var id } && id != user.Id
                ? id
                : (Guid?)null;
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_sign_off_updated",
            request.OperationKey,
            before,
            SignOffSnapshot(user, newDefaultId),
            timeProvider.GetUtcNow(),
            request.Reason);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            EfStaffAccountQueries.Summary(
                user,
                roles),
            WasReplay: false);
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
        return EfStaffAccountQueries.Summary(user, roles.OrderBy(role => role).ToArray());
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

    private async Task<(long Authorizations, long Tokens)> RevokeAuthorizationsAndTokensAsync(
        Guid staffId,
        bool scrubTokenMaterial,
        CancellationToken cancellationToken)
    {
        var subject = staffId.ToString("D");
        var authorizations = await context.Set<OpenIddictEntityFrameworkCoreAuthorization>()
            .Where(item => item.Subject == subject
                && item.Status != OpenIddictConstants.Statuses.Revoked)
            .ToListAsync(cancellationToken);
        var tokens = await context.Set<OpenIddictEntityFrameworkCoreToken>()
            .Where(item => item.Subject == subject
                && item.Status != OpenIddictConstants.Statuses.Revoked)
            .ToListAsync(cancellationToken);
        foreach (var authorization in authorizations)
        {
            authorization.Status = OpenIddictConstants.Statuses.Revoked;
            authorization.ConcurrencyToken = Guid.NewGuid().ToString("N");
            if (scrubTokenMaterial)
            {
                authorization.Properties = null;
                authorization.Scopes = null;
            }
        }

        foreach (var token in tokens)
        {
            token.Status = OpenIddictConstants.Statuses.Revoked;
            token.ConcurrencyToken = Guid.NewGuid().ToString("N");
            if (scrubTokenMaterial)
            {
                token.Payload = null;
                token.Properties = null;
                token.ReferenceId = null;
            }
        }

        return (authorizations.Count, tokens.Count);
    }

    private void ClearExternalCredentialsAndSessions(Guid staffId)
    {
        foreach (var credential in context.Set<UserExternalCredentialEntity>()
                     .Where(item => item.UserId == staffId))
        {
            credential.Enabled = false;
            credential.ProtectedCredential = string.Empty;
            credential.CredentialGeneration++;
            credential.Version++;
            credential.ConcurrencyToken = Guid.NewGuid();
        }

        foreach (var session in context.Set<GlassRepairEstimateSessionEntity>()
                     .Where(item => item.UserId == staffId))
        {
            session.State = Pegasus.Core.Assessment.GlassRepairEstimateSessionState.Cancelled;
            session.ActiveAccountKey = null;
            session.ProtectedSession = string.Empty;
            session.Version++;
            session.ConcurrencyToken = Guid.NewGuid();
            session.UpdatedAtUtc = timeProvider.GetUtcNow();
        }
    }

    private static void EnsureReplay(
        ActionHistoryEntity replay,
        Guid staffId,
        string eventKind,
        string reason)
    {
        if (replay.AggregateId != staffId.ToString("D")
            || replay.EventKind != eventKind
            || !string.Equals(replay.Reason, reason, StringComparison.Ordinal))
        {
            throw OperationConflict();
        }
    }

    private static string GenerateTemporaryPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', 'A')
            .Replace('/', 'b');

    private async Task<StaffRole[]> GetRolesAsync(PegasusIdentityUser user)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        return roleNames.Select(EfStaffAccountQueries.ParseRole).OrderBy(role => role).ToArray();
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

    private static bool RecordedSignOffEqual(
        string? afterJson,
        UpdateStaffAccountSignOffRequest request)
    {
        if (afterJson is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(afterJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("IsSignOffEngineer", out var flag)
            || flag.GetBoolean() != request.IsSignOffEngineer
            || !JsonTextEquals(root, "SignOffPrintedName", request.PrintedName)
            || !JsonTextEquals(root, "SignOffQualifications", request.Qualifications)
            || !root.TryGetProperty("IsDefaultSignOffEngineer", out var isDefault)
            || isDefault.GetBoolean() != request.IsDefault)
        {
            return false;
        }

        return request.Signature is null
            || JsonTextEquals(
                root,
                "SignOffSignatureDigest",
                SignatureDigest(request.Signature));
    }

    private static bool JsonTextEquals(
        JsonElement root,
        string propertyName,
        string? expected)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        var actual = property.ValueKind == JsonValueKind.Null
            ? null
            : property.GetString();
        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static string SignOffSnapshot(
        PegasusIdentityUser user,
        Guid? defaultSignOffEngineerId) =>
        JsonSerializer.Serialize(new
        {
            user.IsSignOffEngineer,
            user.SignOffPrintedName,
            user.SignOffQualifications,
            HasSignOffSignature = user.SignOffSignature is { Length: > 0 },
            user.SignOffSignatureDigest,
            user.IsDefaultSignOffEngineer,
            DefaultSignOffEngineerId = defaultSignOffEngineerId
        });

    private static string SignatureDigest(byte[] signature) =>
        Convert.ToHexString(SHA256.HashData(signature)).ToLowerInvariant();

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

    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException
                || current is SqlException { Number: 1205 or 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }

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
