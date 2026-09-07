using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>Which invariant a Glass's session write ran into.</summary>
public enum GlassRepairEstimateSessionConflict
{
    /// <summary>
    /// The external Glass's account already holds a live session. The
    /// provider allows one, so the database allows one.
    /// </summary>
    ActiveAccount,

    /// <summary>The session moved on since the caller read it.</summary>
    Version,

    /// <summary>
    /// The write carries a different callback fingerprint from the one this
    /// session recorded, so it is not this session's callback.
    /// </summary>
    Callback,

    /// <summary>
    /// The operation key is already held by a different Case or user, so this
    /// is a key collision rather than a replay of the same launch.
    /// </summary>
    OperationKey,
}

/// <summary>
/// A Glass's session write refused because it would break an invariant the
/// store keeps. It changes nothing: the recorded session stands.
/// </summary>
public sealed class GlassRepairEstimateSessionConflictException(
    GlassRepairEstimateSessionConflict conflict, Guid sessionId, string message)
    : InvalidOperationException(message)
{
    public GlassRepairEstimateSessionConflict Conflict { get; } = conflict;

    public Guid SessionId { get; } = sessionId;
}

/// <summary>
/// Persists Glass's repair-estimate sessions over the Foundation
/// <c>GlassRepairEstimateSessions</c> table.
/// </summary>
/// <remarks>
/// <para>
/// <b>One live session per external account.</b> A Glass's account can hold
/// one ERE calculation open at a time, so Pegasus holds one live session for
/// it — across every Pegasus user and every credential generation, because the
/// constraint belongs to the provider's account and not to who typed it in.
/// The account is reduced to <see cref="NormalizeAccountKey"/>'s one-way key
/// and written to <c>ActiveAccountKey</c> only while the session is
/// <see cref="GlassRepairEstimateSessionState.Prepared"/>,
/// <see cref="GlassRepairEstimateSessionState.Launching"/> or
/// <see cref="GlassRepairEstimateSessionState.Active"/>, so the filtered
/// unique index is the rule and this class is only its translator: the
/// database refuses the second live session and that refusal surfaces as
/// <see cref="GlassRepairEstimateSessionConflict.ActiveAccount"/> rather than
/// being retried, swallowed or pre-empted by a read this store could lose a
/// race to.
/// </para>
/// <para>
/// <b>Replay and the callback.</b> <c>OperationKey</c> is unique, so relaunching
/// the same operation returns the session that launch already created instead
/// of creating a second one. <c>CallbackDigest</c> is the fingerprint of the
/// callback this session will accept and it never changes: a write carrying a
/// different one is refused whole. The digest is consumed once — the first
/// write that takes the session out of the live set stamps
/// <c>CallbackConsumedAtUtc</c> — so a caller can tell a callback that has
/// already been acted on from one that has not, and read the recorded result
/// instead of acting on it twice.
/// </para>
/// <para>
/// <b>Protected material.</b> <c>ProtectedSession</c> is stored exactly as the
/// caller hands it over. Protecting and unprotecting it is the caller's, and
/// this store never sees, derives or logs the clear text — nor the account
/// password, which contributes nothing to the account key.
/// </para>
/// <para>
/// Every write is a short serializable transaction, the shape
/// <c>EfCaseReportGenerationStore</c> uses, and concurrency is the row's own
/// <c>Version</c> and <c>ConcurrencyToken</c> rather than the Case's.
/// </para>
/// </remarks>
public sealed class EfGlassRepairEstimateSessionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IGlassRepairEstimateSessionStore
{
    /// <summary>
    /// Domain separation for the account key, not a secret: it keeps the
    /// stored key meaningless outside this application without ever standing
    /// in for one. Changing it re-keys every session, so it is versioned.
    /// </summary>
    private const string AccountKeyDomain = "pegasus.glass-repair-estimate.account-key.v1:";

    /// <summary>The states in which the session holds its account's one live slot.</summary>
    private static readonly GlassRepairEstimateSessionState[] LiveStates =
    [
        GlassRepairEstimateSessionState.Prepared,
        GlassRepairEstimateSessionState.Launching,
        GlassRepairEstimateSessionState.Active,
    ];

    /// <summary>
    /// The one-way key an external Glass's account is recorded under: trimmed,
    /// lower-cased invariantly and compatibility-composed so the same account
    /// typed differently is the same account, then hashed. The account's
    /// password contributes nothing — the key identifies the account, it does
    /// not authenticate it.
    /// </summary>
    public static string NormalizeAccountKey(string externalAccount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalAccount);
        var normalized = externalAccount.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormKC);
        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(AccountKeyDomain + normalized)));
    }

    public async Task<GlassRepairEstimateSessionMaterial?> GetAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        return entity is null ? null : ToMaterial(entity);
    }

    public async Task<GlassRepairEstimateSessionMaterial> CreateAsync(
        GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);
        var session = material.Session;
        ArgumentException.ThrowIfNullOrWhiteSpace(session.OperationKey);
        var operationKey = session.OperationKey.Trim();
        var accountKey = NormalizeAccountKey(session.NormalizedExternalAccountKey);
        var callbackDigest = Digest(material.CallbackDigest);
        ArgumentException.ThrowIfNullOrEmpty(material.ProtectedProviderState);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        if (await FindReplayAsync(context, operationKey, cancellationToken) is { } replay)
        {
            return RequireSameLaunch(replay, session, operationKey);
        }

        var now = timeProvider.GetUtcNow();
        var entity = new GlassRepairEstimateSessionEntity
        {
            Id = session.Id,
            CaseId = session.CaseId,
            UserId = session.PegasusUserId,
            CredentialGeneration = session.CredentialGeneration,
            NormalizedAccountKey = accountKey,
            ActiveAccountKey = IsLive(session.State) ? accountKey : null,
            OperationKey = operationKey,
            State = session.State,
            CreatedAtUtc = session.CreatedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            UpdatedAtUtc = now,
            ProviderVehicleId = session.ProviderVehicleId,
            EreId = session.ProviderEstimateId,
            CallbackDigest = callbackDigest,
            ProtectedSession = material.ProtectedProviderState,
            LastError = session.FailureCode,
            Version = session.Version,
        };
        context.Add(entity);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // The index, not a read this store could have lost a race to,
            // decided. Which index it was decides what the caller is told,
            // and the losing insert releases its locks before that is read.
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            var replayed = await FindReplayAsync(context, operationKey, cancellationToken);
            return replayed is not null
                ? RequireSameLaunch(replayed, session, operationKey)
                : throw new GlassRepairEstimateSessionConflictException(
                    GlassRepairEstimateSessionConflict.ActiveAccount,
                    session.Id,
                    "The Glass's account already holds a live session.");
        }

        return ToMaterial(entity);
    }

    public async Task SaveAsync(
        GlassRepairEstimateSessionMaterial material, long expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);
        var session = material.Session;
        var callbackDigest = Digest(material.CallbackDigest);
        ArgumentException.ThrowIfNullOrEmpty(material.ProtectedProviderState);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .SingleOrDefaultAsync(item => item.Id == session.Id, cancellationToken)
            // The table refuses deletes, so a missing row is an id that was
            // never created rather than a session that moved on.
            ?? throw new ArgumentException(
                $"There is no Glass's session {session.Id}.", nameof(material));

        RequireSameSession(entity, session);
        if (!string.Equals(entity.CallbackDigest, callbackDigest, StringComparison.Ordinal))
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Callback,
                session.Id,
                "The write carries a different callback from the one this Glass's session recorded.");
        }
        if (entity.Version != expectedVersion)
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Version,
                session.Id,
                $"The Glass's session is at version {entity.Version} and not {expectedVersion}.");
        }

        var now = timeProvider.GetUtcNow();
        var wasLive = IsLive(entity.State);
        var live = IsLive(session.State);
        entity.State = session.State;
        entity.ActiveAccountKey = live ? entity.NormalizedAccountKey : null;
        entity.ExpiresAtUtc = session.ExpiresAtUtc;
        entity.ProviderVehicleId = session.ProviderVehicleId;
        entity.EreId = session.ProviderEstimateId;
        entity.LastError = session.FailureCode;
        entity.ProtectedSession = material.ProtectedProviderState;
        entity.UpdatedAtUtc = now;
        entity.Version = expectedVersion + 1;
        // Consumed by the write that takes the session out of the live set,
        // and only once: leaving the live set again never moves the moment
        // the callback was acted on.
        if (wasLive && !live && entity.CallbackConsumedAtUtc is null)
        {
            entity.CallbackConsumedAtUtc = now;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Version,
                session.Id,
                $"The Glass's session moved past version {expectedVersion} while it was being written.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.ActiveAccount,
                session.Id,
                "The Glass's account already holds a live session.");
        }
    }

    private static Task<GlassRepairEstimateSessionEntity?> FindReplayAsync(
        PegasusDbContext context, string operationKey, CancellationToken cancellationToken) =>
        context.Set<GlassRepairEstimateSessionEntity>()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);

    /// <summary>
    /// A replayed operation key must name the same launch. A different Case
    /// or user under the same key is a collision, and returning the other
    /// launch's session would hand one Case's provider material to another.
    /// </summary>
    private static GlassRepairEstimateSessionMaterial RequireSameLaunch(
        GlassRepairEstimateSessionEntity replay, GlassRepairEstimateSession session, string operationKey) =>
        replay.CaseId == session.CaseId && replay.UserId == session.PegasusUserId
            ? ToMaterial(replay)
            : throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.OperationKey,
                replay.Id,
                $"Operation key '{operationKey}' already names another Glass's session.");

    /// <summary>
    /// A save names the session it read. Case, user, credential generation and
    /// operation key are the launch's identity and never change. The external
    /// account is fixed at creation and read from the row, so a save neither
    /// restates nor can move it.
    /// </summary>
    private static void RequireSameSession(
        GlassRepairEstimateSessionEntity entity, GlassRepairEstimateSession session)
    {
        if (entity.CaseId != session.CaseId
            || entity.UserId != session.PegasusUserId
            || entity.CredentialGeneration != session.CredentialGeneration
            || !string.Equals(entity.OperationKey, session.OperationKey, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The write names a different Case, user, credential generation or operation "
                + "than the Glass's session it is saving.",
                nameof(session));
        }
    }

    private static GlassRepairEstimateSessionMaterial ToMaterial(GlassRepairEstimateSessionEntity entity) =>
        new(
            new GlassRepairEstimateSession(
                entity.Id,
                entity.CaseId,
                entity.UserId,
                entity.CredentialGeneration,
                entity.NormalizedAccountKey,
                entity.State,
                entity.Version,
                entity.OperationKey,
                entity.CreatedAtUtc,
                entity.ExpiresAtUtc,
                entity.ProviderVehicleId,
                entity.EreId,
                entity.LastError),
            entity.ProtectedSession,
            entity.CallbackDigest);

    private static bool IsLive(GlassRepairEstimateSessionState state) => Array.IndexOf(LiveStates, state) >= 0;

    private static string Digest(string callbackDigest)
    {
        var digest = callbackDigest.Trim().ToLowerInvariant();
        return digest is { Length: 64 } && digest.All(Uri.IsHexDigit)
            ? digest
            : throw new ArgumentException(
                "A Glass's session requires its callback's SHA-256 digest.", nameof(callbackDigest));
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.GetBaseException() is SqlException { Number: 2601 or 2627 };
}
