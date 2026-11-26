using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Brego;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The integrations' logins (R1288, ADR-0091): one row each, its values
/// protected with the Web host's key ring as a member of staff's Glass's login
/// is (ADR-0043), never in plain text and never shown back. Providers read a
/// login on each call; nothing is cached (a read is about 2 ms against a press
/// of a second or more). An Administrator replaces a login, and each
/// replacement is one history row naming the login, its generation and
/// version, never a value. Brego's stored sign-in lives in its row and is
/// renewed in place (<see cref="IBregoSessionStore"/>); an Administrator
/// signs the account in on the Integrations page (<see cref="BregoAccountLink"/>,
/// R1290), and a sign-in that waits for Brego's e-mailed code is kept in the
/// row's pending columns until it is finished or expires. Every call takes
/// its own context from the factory, so the singleton providers may hold it.
/// </summary>
public sealed class EfIntegrationLoginStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IDataProtectionProvider dataProtection,
    TimeProvider timeProvider) : IIntegrationLoginAdministration, IBregoSessionStore
{
    private const string ProtectionPurpose = "Pegasus.IntegrationLogin.v1";
    private const string PendingPurpose = "pending";
    private const string AggregateType = "integration_login";
    private const int MaximumValueLength = 4096;

    private static readonly IntegrationLogin[] AllLogins = Enum.GetValues<IntegrationLogin>();

    public async Task<IReadOnlyList<IntegrationLoginStatus>> ListAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.IntegrationLogins
            .AsNoTracking()
            .ToDictionaryAsync(item => item.Id, StringComparer.Ordinal, cancellationToken);
        var statuses = new List<IntegrationLoginStatus>(AllLogins.Length);
        foreach (var login in AllLogins)
        {
            statuses.Add(rows.TryGetValue(RowId(login), out var entity)
                ? Status(login, entity)
                : new IntegrationLoginStatus(login, null, null, 0, Readable: true));
        }

        return statuses;
    }

    public async Task<IntegrationLoginStatus> ReplaceAsync(
        ActionActor actor,
        IntegrationLogin login,
        long expectedVersion,
        IReadOnlyDictionary<IntegrationLoginField, string> values,
        string operationKey,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var payload = Payload(login, values);
        var id = RowId(login);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await LockAsync(context, id, cancellationToken);
        var entity = await context.IntegrationLogins
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if ((entity?.Version ?? 0) != expectedVersion)
        {
            throw new DbUpdateConcurrencyException();
        }

        var before = entity is null ? null : HistoryState(entity);
        entity = NextGeneration(context, entity, id);
        Write(login, entity, payload, actor.SubjectId);
        AddHistory(context, actor, entity, "integration_login_replaced", before, operationKey.Trim());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new IntegrationLoginStatus(
            login,
            payload[IntegrationLoginMaterial.PayloadName(IntegrationLoginFields.AccountNameField(login))],
            entity.UpdatedAtUtc,
            entity.Version,
            Readable: true);
    }

    /// <summary>
    /// One login as its provider reads it on each call: the decrypted values
    /// and their generation, or <see langword="null"/> when the login is not
    /// set or the key ring can no longer read it.
    /// </summary>
    internal async Task<IntegrationLoginMaterial?> ReadAsync(
        IntegrationLogin login,
        CancellationToken cancellationToken)
    {
        var id = RowId(login);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntegrationLogins
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null || entity.ProtectedLogin.Length == 0)
        {
            return null;
        }

        return TryReadPayload(login, entity) is { } payload
            ? new IntegrationLoginMaterial(login, entity.Generation, payload)
            : null;
    }

    /// <summary>
    /// A renewal of Brego's stored sign-in (ADR-0084): it rewrites the row's
    /// protected payload in place while the account is still at
    /// <paramref name="generation"/>, and is not a change to the login, so
    /// neither the version nor the generation moves and no history is written.
    /// </summary>
    public async Task<bool> TrySaveAsync(
        long generation,
        BregoStoredSignIn signIn,
        CancellationToken cancellationToken)
    {
        if (signIn is null || string.IsNullOrWhiteSpace(signIn.RefreshToken))
        {
            return false;
        }

        var id = RowId(IntegrationLogin.Brego);
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await context.IntegrationLogins
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == id && item.Generation == generation,
                    cancellationToken);
            if (entity is null
                || entity.ProtectedLogin.Length == 0
                || TryReadPayload(IntegrationLogin.Brego, entity) is not { } stored)
            {
                return false;
            }

            var payload = new Dictionary<string, string>(stored, StringComparer.Ordinal)
            {
                [IntegrationLoginMaterial.RefreshTokenName] = signIn.RefreshToken.Trim()
            };
            if (string.IsNullOrWhiteSpace(signIn.BregoSessionCookie))
            {
                payload.Remove(IntegrationLoginMaterial.SessionCookieName);
            }
            else
            {
                payload[IntegrationLoginMaterial.SessionCookieName] = signIn.BregoSessionCookie.Trim();
            }

            var protectedLogin = Protect(IntegrationLogin.Brego, generation, payload);
            var concurrencyToken = Guid.NewGuid();
            var updated = await context.IntegrationLogins
                .Where(item => item.Id == id && item.Generation == generation)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.ProtectedLogin, protectedLogin)
                        .SetProperty(item => item.ConcurrencyToken, concurrencyToken),
                    cancellationToken);
            return updated == 1;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Best effort: the running host keeps its session in memory, and
            // the next renewal writes again.
            return false;
        }
    }

    /// <summary>
    /// Brego's account as the Integrations page signs it in (R1290): its
    /// version and generation, e-mail address and saved password, and the
    /// sign-in that waits for a code while it has not expired. None when the
    /// login is not set or the key ring can no longer read it.
    /// </summary>
    internal async Task<BregoLinkState?> ReadBregoLinkAsync(CancellationToken cancellationToken)
    {
        var id = RowId(IntegrationLogin.Brego);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.IntegrationLogins
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null
            || entity.ProtectedLogin.Length == 0
            || TryReadPayload(IntegrationLogin.Brego, entity) is not { } payload
            || !payload.TryGetValue(IntegrationLoginMaterial.PayloadName(IntegrationLoginField.Email), out var email)
            || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var password = payload.GetValueOrDefault(IntegrationLoginMaterial.PayloadName(IntegrationLoginField.Password));
        var pending = entity.PendingLinkExpiresAtUtc is { } expiresAt && expiresAt > timeProvider.GetUtcNow()
            ? TryReadPending(entity)
            : null;
        return new BregoLinkState(
            entity.Version,
            entity.Generation,
            email.Trim(),
            string.IsNullOrWhiteSpace(password) ? null : password,
            pending,
            pending is null ? null : entity.PendingLinkExpiresAtUtc);
    }

    /// <summary>
    /// Keeps a Brego sign-in that waits for Brego's e-mailed code, protected
    /// under its own purpose, until <paramref name="expiresAtUtc"/>. The
    /// login's version moves; its generation and sign-in do not.
    /// </summary>
    internal async Task WriteBregoPendingAsync(
        long expectedVersion,
        Dictionary<string, string> pending,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var id = RowId(IntegrationLogin.Brego);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await LockAsync(context, id, cancellationToken);
        var entity = await context.IntegrationLogins
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if ((entity?.Version ?? 0) != expectedVersion)
        {
            throw new DbUpdateConcurrencyException();
        }

        if (entity is null || entity.ProtectedLogin.Length == 0)
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.NoAccount);
        }

        entity.ProtectedPendingLink = CreatePendingProtector(entity.Generation)
            .Protect(JsonSerializer.Serialize(pending));
        entity.PendingLinkExpiresAtUtc = expiresAtUtc;
        entity.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Stores a Brego sign-in an Administrator finished on the Integrations
    /// page (R1290): the refresh token and the new <c>brego-session</c>
    /// beside the account's e-mail address and password, under the login's
    /// next generation, so no host reuses a session of the one before. Any
    /// sign-in that waited for a code is dropped, and one history row names
    /// the login, its generation and version, never a value.
    /// </summary>
    internal async Task WriteBregoSignInAsync(
        ActionActor actor,
        long expectedVersion,
        BregoStoredSignIn signIn,
        string operationKey,
        CancellationToken cancellationToken)
    {
        RequireAdministrator(actor);
        ArgumentNullException.ThrowIfNull(signIn);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var id = RowId(IntegrationLogin.Brego);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await LockAsync(context, id, cancellationToken);
        var entity = await context.IntegrationLogins
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if ((entity?.Version ?? 0) != expectedVersion)
        {
            throw new DbUpdateConcurrencyException();
        }

        if (entity is null
            || entity.ProtectedLogin.Length == 0
            || TryReadPayload(IntegrationLogin.Brego, entity) is not { } stored)
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.NoAccount);
        }

        var payload = new Dictionary<string, string>(stored, StringComparer.Ordinal)
        {
            [IntegrationLoginMaterial.RefreshTokenName] = signIn.RefreshToken.Trim()
        };
        if (string.IsNullOrWhiteSpace(signIn.BregoSessionCookie))
        {
            payload.Remove(IntegrationLoginMaterial.SessionCookieName);
        }
        else
        {
            payload[IntegrationLoginMaterial.SessionCookieName] = signIn.BregoSessionCookie.Trim();
        }

        var before = HistoryState(entity);
        entity.Generation++;
        entity.Version++;
        Write(IntegrationLogin.Brego, entity, payload, actor.SubjectId);
        AddHistory(context, actor, entity, "integration_login_linked", before, operationKey.Trim());
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// The login's row, or the gap its first write fills, is the first lock,
    /// so a concurrent first write waits instead of inserting it twice.
    /// </summary>
    private static Task<int> LockAsync(
        PegasusDbContext context,
        string id,
        CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [IntegrationLogins] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}",
            cancellationToken);

    /// <summary>A new row at generation 1, or the existing row moved to its next generation.</summary>
    private static IntegrationLoginEntity NextGeneration(
        PegasusDbContext context,
        IntegrationLoginEntity? entity,
        string id)
    {
        if (entity is null)
        {
            entity = new IntegrationLoginEntity
            {
                Id = id,
                ProtectedLogin = string.Empty,
                UpdatedBy = string.Empty,
                Generation = 1,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            };
            context.IntegrationLogins.Add(entity);
            return entity;
        }

        entity.Generation++;
        entity.Version++;
        return entity;
    }

    /// <summary>
    /// Protects the payload under the row's new generation. A replaced
    /// account is linked again, so any link begun for the old one is dropped.
    /// </summary>
    private void Write(
        IntegrationLogin login,
        IntegrationLoginEntity entity,
        Dictionary<string, string> payload,
        string updatedBy)
    {
        entity.ProtectedLogin = Protect(login, entity.Generation, payload);
        entity.ProtectedPendingLink = null;
        entity.PendingLinkExpiresAtUtc = null;
        entity.UpdatedBy = updatedBy;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
    }

    private string Protect(
        IntegrationLogin login,
        long generation,
        Dictionary<string, string> payload) =>
        CreateProtector(login, generation).Protect(JsonSerializer.Serialize(payload));

    private IDataProtector CreateProtector(IntegrationLogin login, long generation) =>
        dataProtection.CreateProtector(
            ProtectionPurpose,
            RowId(login),
            generation.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// A Brego sign-in that waits for a code is protected under its own
    /// purpose, beside the login's generation, so neither payload can be read
    /// as the other.
    /// </summary>
    private IDataProtector CreatePendingProtector(long generation) =>
        dataProtection.CreateProtector(
            ProtectionPurpose,
            RowId(IntegrationLogin.Brego),
            generation.ToString(CultureInfo.InvariantCulture),
            PendingPurpose);

    private Dictionary<string, string>? TryReadPending(IntegrationLoginEntity entity)
    {
        if (string.IsNullOrEmpty(entity.ProtectedPendingLink))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                CreatePendingProtector(entity.Generation).Unprotect(entity.ProtectedPendingLink));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The decrypted payload, or <see langword="null"/> when the key ring that
    /// protected it is gone or the payload is not a login's.
    /// </summary>
    private Dictionary<string, string>? TryReadPayload(
        IntegrationLogin login,
        IntegrationLoginEntity entity)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                CreateProtector(login, entity.Generation).Unprotect(entity.ProtectedLogin));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IntegrationLoginStatus Status(IntegrationLogin login, IntegrationLoginEntity entity)
    {
        if (entity.ProtectedLogin.Length == 0)
        {
            return new IntegrationLoginStatus(login, null, entity.UpdatedAtUtc, entity.Version, Readable: true);
        }

        var payload = TryReadPayload(login, entity);
        return new IntegrationLoginStatus(
            login,
            payload?.GetValueOrDefault(
                IntegrationLoginMaterial.PayloadName(IntegrationLoginFields.AccountNameField(login))),
            entity.UpdatedAtUtc,
            entity.Version,
            Readable: payload is not null);
    }

    /// <summary>
    /// The JSON payload of <paramref name="values"/>: the login's fields and
    /// no other, every required one not blank, and an optional one left out
    /// or blank not stored (Brego's password, R1290). A name is trimmed; a
    /// password or secret is kept exactly as entered.
    /// </summary>
    private static Dictionary<string, string> Payload(
        IntegrationLogin login,
        IReadOnlyDictionary<IntegrationLoginField, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var fields = IntegrationLoginFields.Of(login);
        foreach (var given in values.Keys)
        {
            if (!fields.Contains(given))
            {
                throw new ArgumentException(
                    "Exactly the login's own fields are required.",
                    nameof(values));
            }
        }

        var payload = new Dictionary<string, string>(fields.Count + 2, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!IntegrationLoginFields.IsRequired(login, field)
                && (!values.TryGetValue(field, out var optional) || string.IsNullOrWhiteSpace(optional)))
            {
                continue;
            }

            if (!values.TryGetValue(field, out var value)
                || string.IsNullOrWhiteSpace(value)
                || value.Length > MaximumValueLength)
            {
                throw new ArgumentException(
                    $"A non-empty {field} no longer than {MaximumValueLength} characters is required.",
                    nameof(values));
            }

            payload[IntegrationLoginMaterial.PayloadName(field)] =
                field is IntegrationLoginField.Password or IntegrationLoginField.ClientSecret
                    ? value
                    : value.Trim();
        }

        return payload;
    }

    private static object HistoryState(IntegrationLoginEntity entity) =>
        new { entity.Id, entity.Generation, entity.Version };

    private static void AddHistory(
        PegasusDbContext context,
        ActionActor actor,
        IntegrationLoginEntity entity,
        string eventKind,
        object? before,
        string correlationId)
    {
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.Id,
            EventKind = eventKind,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role)),
            OccurredAtUtc = entity.UpdatedAtUtc,
            Outcome = "succeeded",
            CorrelationId = correlationId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(HistoryState(entity))
        });
    }

    private static void RequireAdministrator(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
    }

    /// <summary>The login's row identifier, which is also part of its protection purpose.</summary>
    internal static string RowId(IntegrationLogin login) =>
        login switch
        {
            IntegrationLogin.GlassValuation => "glass-valuation",
            IntegrationLogin.Cazana => "cazana",
            IntegrationLogin.Brego => "brego",
            IntegrationLogin.ExperianHistory => "experian-history",
            IntegrationLogin.ExperianCap => "experian-cap",
            _ => throw new ArgumentOutOfRangeException(nameof(login))
        };
}
