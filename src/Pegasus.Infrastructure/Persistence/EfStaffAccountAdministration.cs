using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfStaffAccountAdministration(
    PegasusDbContext context,
    UserManager<PegasusIdentityUser> userManager,
    TimeProvider timeProvider)
    : ICreateStaffAccountStore,
      IDisableStaffAccountStore,
      IUpdateStaffAccountSettingsStore,
      IEnableStaffAccountStore,
      IForceStaffLogoutStore,
      IResetStaffPasswordStore,
      IDeleteStaffAccountStore
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

            var replayRole = await GetRoleAsync(replayUser);
            await transaction.CommitAsync(cancellationToken);
            return new(
                EfStaffAccountQueries.Summary(replayUser, replayRole),
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
            StaffRole.User,
            "staff_account_created",
            request.OperationKey,
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
            var replayRole = await GetRoleAsync(replayUser);
            var replayCounts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(
                EfStaffAccountQueries.Summary(
                    replayUser,
                    replayRole),
                replayCounts.Authorizations,
                replayCounts.Tokens,
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var role = await GetRoleAsync(user);
        await EfEditScopeStore.RequireAsync(
            context, EditScopeKind.StaffAccount, user.Id, user.Version,
            request.ExpectedVersion, request.Actor, request.EditLeaseToken,
            timeProvider.GetUtcNow(), cancellationToken);
        if (user.IsEnabled
            && role == StaffRole.Administrator
            && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.LastAdministrator);
        }

        var before = Snapshot(user, role);
        user.IsEnabled = false;
        user.Version++;
        var revoked = (Authorizations: 0L, Tokens: 0L);
        if (before != Snapshot(user, role))
        {
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        }
        revoked = await RevokeAuthorizationsAndTokensAsync(
            user.Id,
            scrubTokenMaterial: false,
            cancellationToken);

        await EfEditScopeStore.ClearForActorAsync(
            context,
            ActionActor.Staff(user.Id, [role]),
            cancellationToken);

        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_disabled",
            request.OperationKey,
            before,
            Snapshot(user, role, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.SecurityStampChanged,
            user.Id.ToString("D"),
            request.Actor,
            request.OperationKey,
            "staff_account_disabled",
            now);
        EfEditScopeStore.Complete(context, EditScopeKind.StaffAccount, user.Id);
        await context.SaveChangesAsync(cancellationToken);
        await InvalidateChangedSignatoriesAsync(now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            EfStaffAccountQueries.Summary(
                user,
                role),
            revoked.Authorizations,
            revoked.Tokens,
            WasReplay: false);
    }

    public async Task<UpdateStaffAccountSettingsResult> UpdateAsync(
        UpdateStaffAccountSettingsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await UpdateSettingsCoreAsync(request, cancellationToken);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            throw OperationConflict();
        }
    }

    private async Task<UpdateStaffAccountSettingsResult> UpdateSettingsCoreAsync(
        UpdateStaffAccountSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var replay = await FindOperationAsync(request.OperationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.AggregateId != request.StaffId.ToString("D")
                || replay.EventKind != "staff_account_settings_updated"
                || !string.Equals(replay.Reason, request.Reason, StringComparison.Ordinal)
                || !RecordedSettingsEqual(replay.AfterJson, request))
            {
                throw OperationConflict();
            }

            var replayUser = await FindUserAsync(request.StaffId, cancellationToken);
            var replayCounts = ParseRevocationCounts(replay.AfterJson);
            await transaction.CommitAsync(cancellationToken);
            return new(
                EfStaffAccountQueries.Summary(
                    replayUser,
                    await GetRoleAsync(replayUser)),
                replayCounts.Authorizations,
                replayCounts.Tokens,
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var currentRole = await GetRoleAsync(user);
        await EfEditScopeStore.RequireAsync(
            context,
            EditScopeKind.StaffAccount,
            user.Id,
            user.Version,
            request.ExpectedVersion,
            request.Actor,
            request.EditLeaseToken,
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (user.IsEnabled
            && currentRole == StaffRole.Administrator
            && request.Role != StaffRole.Administrator
            && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.LastAdministrator);
        }

        var before = Snapshot(user, currentRole);
        var roleChanged = currentRole != request.Role;
        if (roleChanged
            && Guid.TryParse(request.Actor.SubjectId, out var actorId)
            && actorId == user.Id)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.SelfAction);
        }
        var revoked = (Authorizations: 0L, Tokens: 0L);
        if (roleChanged)
        {
            ThrowIfFailed(await userManager.RemoveFromRoleAsync(user, RoleName(currentRole)));
            ThrowIfFailed(await userManager.AddToRoleAsync(user, RoleName(request.Role)));
            ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
            revoked = await RevokeAuthorizationsAndTokensAsync(
                user.Id,
                scrubTokenMaterial: false,
                cancellationToken);
        }

        var previousDefault = await context.Users.SingleOrDefaultAsync(
            item => item.IsDefaultSignOffEngineer,
            cancellationToken);
        ApplySignOffSettings(user, request, previousDefault);
        if (previousDefault is not null
            && previousDefault.Id != user.Id
            && !previousDefault.IsDefaultSignOffEngineer)
        {
            previousDefault.Version++;
        }
        var settingsChanged = before != Snapshot(user, request.Role);
        if (settingsChanged)
        {
            user.Version++;
        }

        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_settings_updated",
            request.OperationKey,
            before,
            Snapshot(user, request.Role, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        if (roleChanged)
        {
            AddSecurityEvent(
                SecurityEventType.SecurityStampChanged,
                user.Id.ToString("D"),
                request.Actor,
                request.OperationKey,
                "staff_account_settings_updated",
                now);
            await EfEditScopeStore.ClearForActorAsync(
                context,
                ActionActor.Staff(user.Id, [request.Role]),
                cancellationToken);
        }

        EfEditScopeStore.Complete(context, EditScopeKind.StaffAccount, user.Id);
        await context.SaveChangesAsync(cancellationToken);
        if (settingsChanged)
        {
            await InvalidateChangedSignatoriesAsync(now, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new(
            EfStaffAccountQueries.Summary(
                user,
                request.Role),
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
                EfStaffAccountQueries.Summary(replayUser, await GetRoleAsync(replayUser)),
                WasReplay: true);
        }

        var user = await FindUserAsync(request.StaffId, cancellationToken);
        var role = await GetRoleAsync(user);
        await EfEditScopeStore.RequireAsync(
            context, EditScopeKind.StaffAccount, user.Id, user.Version,
            request.ExpectedVersion, request.Actor, request.EditLeaseToken,
            timeProvider.GetUtcNow(), cancellationToken);
        var before = Snapshot(user, role);
        user.IsEnabled = true;
        user.Version++;
        if (before != Snapshot(user, role))
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
            Snapshot(user, role),
            now,
            request.Reason);
        await EfEditScopeStore.ClearForActorAsync(
            context, ActionActor.Staff(user.Id, [role]), cancellationToken);
        EfEditScopeStore.Complete(context, EditScopeKind.StaffAccount, user.Id);
        await context.SaveChangesAsync(cancellationToken);
        await InvalidateChangedSignatoriesAsync(now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(EfStaffAccountQueries.Summary(user, role), WasReplay: false);
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
        var role = await GetRoleAsync(user);
        await EfEditScopeStore.RequireAsync(
            context, EditScopeKind.StaffAccount, user.Id, user.Version,
            request.ExpectedVersion, request.Actor, request.EditLeaseToken,
            timeProvider.GetUtcNow(), cancellationToken);
        var before = Snapshot(user, role);
        user.Version++;
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
            Snapshot(user, role, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.SecurityStampChanged,
            user.Id.ToString("D"),
            request.Actor,
            request.OperationKey,
            "staff_logout_forced",
            now);
        await EfEditScopeStore.ClearForActorAsync(
            context, ActionActor.Staff(user.Id, [role]), cancellationToken);
        EfEditScopeStore.Complete(context, EditScopeKind.StaffAccount, user.Id);
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
        var role = await GetRoleAsync(user);
        await EfEditScopeStore.RequireAsync(
            context, EditScopeKind.StaffAccount, user.Id, user.Version,
            request.ExpectedVersion, request.Actor, request.EditLeaseToken,
            timeProvider.GetUtcNow(), cancellationToken);
        var before = Snapshot(user, role);
        user.Version++;
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
            Snapshot(user, role, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.PasswordChanged,
            user.Id.ToString("D"),
            request.Actor,
            request.OperationKey,
            "staff_password_reset",
            now);
        await EfEditScopeStore.ClearForActorAsync(
            context, ActionActor.Staff(user.Id, [role]), cancellationToken);
        EfEditScopeStore.Complete(context, EditScopeKind.StaffAccount, user.Id);
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
        var role = await GetRoleAsync(user);
        await EfEditScopeStore.RequireAsync(
            context, EditScopeKind.StaffAccount, user.Id, user.Version,
            request.ExpectedVersion, request.Actor, request.EditLeaseToken,
            timeProvider.GetUtcNow(), cancellationToken);
        if (user.IsEnabled
            && role == StaffRole.Administrator
            && await CountEnabledAdministratorsAsync(cancellationToken) <= 1)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.LastAdministrator);
        }

        var before = Snapshot(user, role);
        user.IsEnabled = false;
        user.Version++;
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
        await EfEditScopeStore.ClearForActorAsync(
            context, ActionActor.Staff(user.Id, [role]), cancellationToken);
        var now = timeProvider.GetUtcNow();
        AddHistory(
            request.Actor,
            user.Id,
            "staff_account_deleted",
            request.OperationKey,
            before,
            Snapshot(user, role, revoked.Authorizations, revoked.Tokens),
            now,
            request.Reason);
        AddSecurityEvent(
            SecurityEventType.SecurityStampChanged,
            user.Id.ToString("D"),
            request.Actor,
            request.OperationKey,
            "staff_account_deleted",
            now);
        EfEditScopeStore.Complete(context, EditScopeKind.StaffAccount, user.Id);
        await context.SaveChangesAsync(cancellationToken);
        await InvalidateChangedSignatoriesAsync(now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            user.Id,
            revoked.Authorizations,
            revoked.Tokens,
            CredentialsCleared: true,
            WasReplay: false);
    }

    private async Task InvalidateChangedSignatoriesAsync(
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        // The profile writes above are saved inside the still-open transaction,
        // so the ordinary eligible-profile query sees exactly the new tuple.
        await EfCaseReportGenerationStore.MarkChangedSignatoriesStaleAsync(
            context, nowUtc, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<StaffAccountSummary> CreateUserCoreAsync(
        ActionActor actor,
        string userName,
        string temporaryPassword,
        StaffRole role,
        string eventKind,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var normalizedUserName = NormalizeUserName(userName);
        {
            var normalizedRoleName = RoleName(role).ToUpperInvariant();
            if (!await context.Roles.AnyAsync(
                    item => item.NormalizedName == normalizedRoleName,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "The required staff role has not been initialized.");
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
        ThrowIfFailed(await userManager.AddToRoleAsync(user, RoleName(role)));

        AddHistory(
            actor,
            user.Id,
            eventKind,
            operationKey,
            beforeJson: null,
            Snapshot(user, role),
            timeProvider.GetUtcNow());
        return EfStaffAccountQueries.Summary(user, role);
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

    private async Task<StaffRole> GetRoleAsync(PegasusIdentityUser user)
    {
        var roleNames = await userManager.GetRolesAsync(user);
        return roleNames.Count == 1
            ? EfStaffAccountQueries.ParseRole(roleNames.Single())
            : throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.InvalidAccount);
    }

    private static void ApplySignOffSettings(
        PegasusIdentityUser user,
        UpdateStaffAccountSettingsRequest request,
        PegasusIdentityUser? previousDefault)
    {
        if (request.IsDefaultSignOffEngineer
            && !SignOffEngineerEligibility.IsEligible(
                user.IsEnabled,
                request.Role,
                request.IsSignOffEngineer,
                request.Signature ?? user.SignOffSignature))
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.IneligibleSignOffEngineer);
        }

        user.IsSignOffEngineer = request.IsSignOffEngineer;
        user.SignOffPrintedName = request.IsSignOffEngineer ? request.PrintedName : null;
        user.SignOffQualifications = request.IsSignOffEngineer ? request.Qualifications : null;
        if (request.Signature is not null)
        {
            user.SignOffSignature = request.Signature;
            user.SignOffSignatureDigest = SignatureDigest(request.Signature);
        }

        if (!request.IsSignOffEngineer)
        {
            user.SignOffSignature = null;
            user.SignOffSignatureDigest = null;
        }

        if (request.IsDefaultSignOffEngineer
            && previousDefault is not null
            && previousDefault.Id != user.Id)
        {
            previousDefault.IsDefaultSignOffEngineer = false;
        }

        user.IsDefaultSignOffEngineer = request.IsDefaultSignOffEngineer;
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

    /// <summary>
    /// Records a security event about a staff account. <paramref name="subjectId"/>
    /// is the account the change landed on; <paramref name="actor"/> is the
    /// operator who made it, so the Action logs view names a person rather than
    /// reading the target back as the actor.
    /// </summary>
    private void AddSecurityEvent(
        SecurityEventType type,
        string subjectId,
        ActionActor actor,
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
            ReasonCode = reasonCode,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId
        });
    }

    private static string Snapshot(
        PegasusIdentityUser user,
        StaffRole role,
        long revokedAuthorizations = 0,
        long revokedTokens = 0) =>
        JsonSerializer.Serialize(new
        {
            user.Id,
            user.UserName,
            user.IsEnabled,
            user.MustChangePassword,
            Role = RoleName(role),
            user.IsSignOffEngineer,
            user.SignOffPrintedName,
            user.SignOffQualifications,
            user.SignOffSignatureDigest,
            user.IsDefaultSignOffEngineer,
            RevokedAuthorizations = revokedAuthorizations,
            RevokedTokens = revokedTokens
        });

    private static bool RecordedSettingsEqual(
        string? afterJson,
        UpdateStaffAccountSettingsRequest request)
    {
        if (afterJson is null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(afterJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("Role", out var roleElement)
            || roleElement.ValueKind != JsonValueKind.String
            || EfStaffAccountQueries.ParseRole(roleElement.GetString() ?? string.Empty) != request.Role
            || !root.TryGetProperty("IsSignOffEngineer", out var signOff)
            || signOff.GetBoolean() != request.IsSignOffEngineer
            || !JsonTextEquals(root, "SignOffPrintedName", request.IsSignOffEngineer ? request.PrintedName : null)
            || !JsonTextEquals(root, "SignOffQualifications", request.IsSignOffEngineer ? request.Qualifications : null)
            || !root.TryGetProperty("IsDefaultSignOffEngineer", out var defaultSignOff)
            || defaultSignOff.GetBoolean() != request.IsDefaultSignOffEngineer)
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
