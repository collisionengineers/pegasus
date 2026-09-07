using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfPerUserExternalCredentialStore(
    PegasusDbContext context,
    IDataProtectionProvider dataProtection,
    TimeProvider timeProvider)
    : IPerUserExternalCredentialReader,
      IPerUserExternalCredentialAdministration
{
    private const string ProtectionPurpose = "Pegasus.PerUserExternalCredential.v1";
    private const int MaximumExternalUsernameLength = 256;
    private const int MaximumExternalPasswordLength = 4096;

    public async Task<PerUserExternalCredentialMaterial?> GetEnabledAsync(
        ActionActor actor,
        ExternalCredentialProvider provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);
        if (actor.Kind != ActorKind.Staff
            || !Guid.TryParse(actor.SubjectId, out var staffId))
        {
            throw new StaffAuthorizationException(StaffAccessRight.AccessStaffApplication);
        }

        var userEnabled = await context.Users
            .AsNoTracking()
            .AnyAsync(item => item.Id == staffId && item.IsEnabled, cancellationToken);
        if (!userEnabled)
        {
            return null;
        }

        var entity = await context.Set<UserExternalCredentialEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == staffId
                    && item.Provider == ProviderName(provider)
                    && item.Enabled
                    && item.ProtectedCredential != string.Empty,
                cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var payload = JsonSerializer.Deserialize<CredentialPayload>(
            CreateProtector(provider, staffId, entity.CredentialGeneration)
                .Unprotect(entity.ProtectedCredential))
            ?? throw new CryptographicException("The external credential payload is invalid.");
        return new(
            Reference(entity),
            payload.Username,
            payload.Password);
    }

    public async Task<PerUserExternalCredentialStatus> GetAsync(
        ActionActor actor,
        Guid pegasusUserId,
        ExternalCredentialProvider provider,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(actor, pegasusUserId);
        await RequireUserAsync(pegasusUserId, requireEnabled: false, cancellationToken);
        var entity = await context.Set<UserExternalCredentialEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == pegasusUserId
                    && item.Provider == ProviderName(provider),
                cancellationToken);
        return entity is null || entity.ProtectedCredential.Length == 0
            ? EmptyStatus(pegasusUserId, provider, entity)
            : Status(entity, ReadPayload(entity, provider).Username);
    }

    public async Task<PerUserExternalCredentialStatus> ReplaceAsync(
        ActionActor actor,
        Guid pegasusUserId,
        ExternalCredentialProvider provider,
        long expectedVersion,
        string username,
        string password,
        bool enabled,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(actor, pegasusUserId);
        username = NormalizeRequiredCredentialValue(
            username,
            MaximumExternalUsernameLength,
            nameof(username));
        _ = NormalizeRequiredCredentialValue(
            password,
            MaximumExternalPasswordLength,
            nameof(password),
            trim: false);
        await RequireUserAsync(pegasusUserId, requireEnabled: true, cancellationToken);

        var entity = await context.Set<UserExternalCredentialEntity>()
            .SingleOrDefaultAsync(
                item => item.UserId == pegasusUserId
                    && item.Provider == ProviderName(provider),
                cancellationToken);
        if (entity is null)
        {
            if (expectedVersion != 0)
            {
                throw new DbUpdateConcurrencyException();
            }

            entity = new UserExternalCredentialEntity
            {
                Id = Guid.NewGuid(),
                Provider = ProviderName(provider),
                UserId = pegasusUserId,
                NormalizedAccountKey = NormalizeAccountKey(provider, username),
                CredentialGeneration = 1,
                ProtectedCredential = string.Empty,
                UpdatedBy = actor.SubjectId,
                UpdatedAtUtc = timeProvider.GetUtcNow(),
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            };
            context.Set<UserExternalCredentialEntity>().Add(entity);
        }
        else
        {
            EnsureVersion(entity.Version, expectedVersion);
            CancelOldSessions(pegasusUserId, entity.CredentialGeneration);
            entity.CredentialGeneration++;
            entity.Version++;
            entity.ConcurrencyToken = Guid.NewGuid();
        }

        entity.Enabled = enabled;
        entity.NormalizedAccountKey = NormalizeAccountKey(provider, username);
        entity.UpdatedBy = actor.SubjectId;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        entity.ProtectedCredential = CreateProtector(
                provider,
                pegasusUserId,
                entity.CredentialGeneration)
            .Protect(JsonSerializer.Serialize(new CredentialPayload(username, password)));
        AddHistory(actor, entity, "external_credential_replaced");
        await context.SaveChangesAsync(cancellationToken);
        return Status(entity, username);
    }

    public async Task ClearAsync(
        ActionActor actor,
        Guid pegasusUserId,
        ExternalCredentialProvider provider,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(actor, pegasusUserId);
        await RequireUserAsync(pegasusUserId, requireEnabled: false, cancellationToken);
        var entity = await context.Set<UserExternalCredentialEntity>()
            .SingleOrDefaultAsync(
                item => item.UserId == pegasusUserId
                    && item.Provider == ProviderName(provider),
                cancellationToken);
        if (entity is null)
        {
            if (expectedVersion != 0)
            {
                throw new DbUpdateConcurrencyException();
            }

            return;
        }

        EnsureVersion(entity.Version, expectedVersion);
        CancelOldSessions(pegasusUserId, entity.CredentialGeneration);
        entity.Enabled = false;
        entity.ProtectedCredential = string.Empty;
        entity.CredentialGeneration++;
        entity.Version++;
        entity.ConcurrencyToken = Guid.NewGuid();
        entity.UpdatedBy = actor.SubjectId;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        AddHistory(actor, entity, "external_credential_cleared");
        await context.SaveChangesAsync(cancellationToken);
    }

    private IDataProtector CreateProtector(
        ExternalCredentialProvider provider,
        Guid userId,
        long generation) =>
        dataProtection.CreateProtector(
            ProtectionPurpose,
            ProviderName(provider),
            userId.ToString("D"),
            generation.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private CredentialPayload ReadPayload(
        UserExternalCredentialEntity entity,
        ExternalCredentialProvider provider) =>
        JsonSerializer.Deserialize<CredentialPayload>(
            CreateProtector(provider, entity.UserId, entity.CredentialGeneration)
                .Unprotect(entity.ProtectedCredential))
        ?? throw new CryptographicException("The external credential payload is invalid.");

    private void CancelOldSessions(Guid userId, long credentialGeneration)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var session in context.Set<GlassRepairEstimateSessionEntity>().Where(
                     item => item.UserId == userId
                         && item.CredentialGeneration == credentialGeneration
                         && item.State != GlassRepairEstimateSessionState.Completed
                         && item.State != GlassRepairEstimateSessionState.Cancelled
                         && item.State != GlassRepairEstimateSessionState.Expired))
        {
            session.State = GlassRepairEstimateSessionState.Cancelled;
            session.ActiveAccountKey = null;
            session.ProtectedSession = string.Empty;
            session.UpdatedAtUtc = now;
            session.Version++;
            session.ConcurrencyToken = Guid.NewGuid();
        }
    }

    private void AddHistory(
        ActionActor actor,
        UserExternalCredentialEntity entity,
        string eventKind)
    {
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = "staff_external_credential",
            AggregateId = entity.UserId.ToString("D"),
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role)),
            OccurredAtUtc = entity.UpdatedAtUtc,
            Outcome = "succeeded",
            CorrelationId = Guid.NewGuid().ToString("N"),
            BeforeJson = null,
            AfterJson = JsonSerializer.Serialize(new
            {
                entity.Provider,
                entity.UserId,
                entity.Enabled,
                entity.CredentialGeneration,
                entity.Version
            })
        });
    }

    private async Task RequireUserAsync(
        Guid userId,
        bool requireEnabled,
        CancellationToken cancellationToken)
    {
        var state = await context.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.IsEnabled })
            .SingleOrDefaultAsync(cancellationToken);
        if (state is null)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.StaffAccountNotFound);
        }

        if (requireEnabled && !state.IsEnabled)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.DisabledAccount);
        }
    }

    private static void RequireAdministrator(ActionActor actor, Guid staffId)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageStaffAccounts);
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staff account identifier is required.",
                nameof(staffId));
        }
    }

    private static string NormalizeRequiredCredentialValue(
        string value,
        int maximumLength,
        string parameterName,
        bool trim = true)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var normalized = trim ? value.Trim() : value;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"A non-empty value no longer than {maximumLength} characters is required.",
                parameterName);
        }

        return normalized;
    }

    private static void EnsureVersion(long actual, long expected)
    {
        if (actual != expected)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    private static string NormalizeAccountKey(
        ExternalCredentialProvider provider,
        string username) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{ProviderName(provider)}\n{username.ToUpperInvariant()}")))
            .ToLowerInvariant();

    private static string ProviderName(ExternalCredentialProvider provider) =>
        provider switch
        {
            ExternalCredentialProvider.GlassRepairEstimate => "glass_repair_estimate",
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

    private static PerUserExternalCredentialReference Reference(
        UserExternalCredentialEntity entity) =>
        new(
            entity.UserId,
            ExternalCredentialProvider.GlassRepairEstimate,
            entity.CredentialGeneration,
            entity.NormalizedAccountKey,
            entity.Enabled,
            entity.Version);

    private static PerUserExternalCredentialStatus Status(
        UserExternalCredentialEntity entity,
        string username) =>
        new(
            entity.UserId,
            ExternalCredentialProvider.GlassRepairEstimate,
            Configured: true,
            entity.Enabled,
            username,
            entity.CredentialGeneration,
            entity.Version,
            entity.UpdatedAtUtc);

    private static PerUserExternalCredentialStatus EmptyStatus(
        Guid userId,
        ExternalCredentialProvider provider,
        UserExternalCredentialEntity? entity) =>
        new(
            userId,
            provider,
            Configured: false,
            Enabled: false,
            Username: null,
            entity?.CredentialGeneration ?? 0,
            entity?.Version ?? 0,
            entity?.UpdatedAtUtc);

    private sealed record CredentialPayload(string Username, string Password);
}
