using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.Infrastructure.Persistence;

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
/// The account arrives already reduced to the one canonical account key —
/// <see cref="Pegasus.Core.Identity.PerUserExternalCredentialReference.NormalizedExternalAccountKey"/>,
/// minted by the credential store that owns that normalization — and this
/// store writes it to <c>ActiveAccountKey</c> unchanged for exactly the states
/// in <see cref="GlassRepairEstimateSessionPolicy.OccupiesAccount"/>, so the filtered unique index is the
/// rule and this class is only its translator: the database refuses the second
/// live session and that refusal surfaces as
/// <see cref="GlassRepairEstimateSessionConflict.ActiveAccount"/> rather than
/// being retried, swallowed or pre-empted by a read this store could lose a
/// race to.
/// </para>
/// <para>
/// <b>The account stays occupied until the provider's side is known to have
/// ended.</b> <see cref="GlassRepairEstimateSessionState.Unknown"/> says the
/// provider's outcome is uncertain, and an uncertain session may still be the
/// one holding that account's calculation open, so it keeps the slot: a new
/// launch for the account is refused rather than raced against the provider.
/// The slot is released only by
/// <see cref="GlassRepairEstimateSessionState.Completed"/>,
/// <see cref="GlassRepairEstimateSessionState.Failed"/>,
/// <see cref="GlassRepairEstimateSessionState.Expired"/> and
/// <see cref="GlassRepairEstimateSessionState.Cancelled"/>.
/// <see cref="GlassRepairEstimateSessionState.AwaitingImport"/> and
/// <see cref="GlassRepairEstimateSessionState.Importing"/> keep it too: Pegasus
/// importing a result says nothing about whether the operator's interactive
/// provider session closed, and the shared contract carries no statement that
/// it did. Releasing the account at import needs that statement added to the
/// contract, not guessed at here.
/// </para>
/// <para>
/// <b>Replay.</b> <c>OperationKey</c> is unique, so relaunching the same
/// operation returns the session that launch already created instead of
/// creating a second one — the index decides that, and <see cref="CreateAsync"/>
/// never reads the key ahead of the insert that would take it. But the recorded
/// session comes back only when the replay names the same launch: the
/// same Case, the same Pegasus user, the same credential generation and the
/// same account key. Anything else under that key is a collision and is
/// refused as <see cref="GlassRepairEstimateSessionConflict.OperationKey"/>,
/// because handing the recorded session back would hand one launch's provider
/// material to another. The protected provider state is never part of that
/// comparison: it is opaque here, and a relaunch legitimately carries fresh
/// cookies for the same launch.
/// </para>
/// <para>
/// <b>The callback.</b> <c>CallbackDigest</c> is the fingerprint of the
/// callback this session will accept and it never changes: a write carrying a
/// different one is refused whole. The digest is consumed once — by the write
/// to <see cref="GlassRepairEstimateSessionState.Importing"/> that claims the
/// callback — so a caller can tell a callback that has already been acted on
/// from a terminal transition that received no callback. That write stamps
/// <c>CallbackConsumedAtUtc</c> and no later write moves it.
/// </para>
/// <para>
/// <b>The material is the row's whole mutable state.</b> <c>ProtectedSession</c>
/// is stored exactly as the caller hands it over, and so is
/// <c>ResultArtifactsJson</c>: both are opaque here — this store never sees,
/// derives, parses or logs what is inside them. The account key is opaque in
/// the same way: it arrives canonical and is written and compared verbatim.
/// A save writes every mutable column
/// from the material it is handed, so a null result, failure code or provider
/// id <i>writes</i> null; null never means "leave what is already there". That
/// is the rule the row's other mutable columns already followed, and it is safe
/// because <see cref="GetAsync"/> returns the results alongside the session: a
/// caller that reads, changes and saves keeps them without restating them.
/// </para>
/// <para>
/// Every write is a short serializable transaction, the shape
/// <c>EfCaseReportGenerationStore</c> uses, and concurrency is the row's own
/// <c>Version</c> and <c>ConcurrencyToken</c> rather than the Case's.
/// </para>
/// </remarks>
public sealed class EfGlassRepairEstimateSessionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IGlassRepairEstimateSessionStore, IGlassRepairEstimateSessionReader
{
    public async Task<GlassRepairEstimateSessionMaterial?> GetAsync(
        Guid sessionId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        return entity is null ? null : ToMaterial(entity);
    }

    /// <summary>
    /// The Engineer's own newest session for a Case. Ordered by the row's own
    /// <c>CreatedAtUtc</c> and then its id, so a Case an Engineer has launched
    /// more than once answers with the latest deterministically rather than
    /// with whichever row the server happened to return first.
    /// </summary>
    public async Task<GlassRepairEstimateSession?> GetForCaseAsync(
        Guid caseId, Guid pegasusUserId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.UserId == pegasusUserId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToSession(entity);
    }

    /// <summary>
    /// The session a one-use correlation names. The token is never stored, so
    /// the row is found by the fingerprint its launch recorded — minted by the
    /// gateway that owns that derivation, never a second time here.
    /// </summary>
    public async Task<GlassRepairEstimateSession?> FindByCallbackAsync(
        string correlation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlation))
        {
            return null;
        }

        var digest = GlassRepairEstimateGateway.CallbackDigestOf(correlation);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.CallbackDigest == digest, cancellationToken);
        return entity is null ? null : ToSession(entity);
    }

    public async Task<GlassRepairEstimateSessionMaterial> CreateAsync(
        GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(material);
        var session = material.Session;
        ArgumentException.ThrowIfNullOrWhiteSpace(session.OperationKey);
        var operationKey = session.OperationKey.Trim();
        // The canonical account key is minted once, by the credential store
        // that owns the external account, and is stored and compared here
        // exactly as it arrives: a second normalization in this layer would be
        // a second owner of the same rule and could disagree with the first.
        ArgumentException.ThrowIfNullOrWhiteSpace(session.NormalizedExternalAccountKey);
        var accountKey = session.NormalizedExternalAccountKey;
        var callbackDigest = Digest(material.CallbackDigest);
        ArgumentException.ThrowIfNullOrEmpty(material.ProtectedProviderState);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        // The insert goes first and nothing reads the operation key ahead of
        // it. A serializable read of a key that is not there takes RangeS-S
        // over the gap it would occupy, and the insert behind it then asks to
        // convert that same range to RangeI-N — so two launches deadlocked on
        // one another over the empty IX_GlassRepairEstimateSessions_OperationKey,
        // whichever accounts and keys they carried. The index was always the
        // decider; asking it first costs a rolled-back insert on the replay
        // path and takes no lock that has to be converted.
        var now = timeProvider.GetUtcNow();
        var entity = new GlassRepairEstimateSessionEntity
        {
            Id = session.Id,
            CaseId = session.CaseId,
            UserId = session.PegasusUserId,
            CredentialGeneration = session.CredentialGeneration,
            NormalizedAccountKey = accountKey,
            ActiveAccountKey = OccupiesAccount(session.State) ? accountKey : null,
            OperationKey = operationKey,
            State = session.State,
            CreatedAtUtc = session.CreatedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            UpdatedAtUtc = now,
            ProviderVehicleId = session.ProviderVehicleId,
            EreId = session.ProviderEstimateId,
            CallbackDigest = callbackDigest,
            ProtectedSession = material.ProtectedProviderState,
            ResultArtifactsJson = material.ResultArtifactsJson,
            LastError = session.FailureCode,
            Version = session.Version,
        };
        context.Add(entity);
        AddHistory(context, entity, null, session.State, now);

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
                ? RequireSameLaunch(replayed, session, accountKey, operationKey)
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
        var previousState = entity.State;
        entity.State = session.State;
        entity.ActiveAccountKey = OccupiesAccount(session.State) ? entity.NormalizedAccountKey : null;
        entity.ExpiresAtUtc = session.ExpiresAtUtc;
        entity.ProviderVehicleId = session.ProviderVehicleId;
        entity.EreId = session.ProviderEstimateId;
        entity.LastError = session.FailureCode;
        entity.ProtectedSession = material.ProtectedProviderState;
        // The material is the row's whole mutable state, so a null result
        // writes null. A caller that means to keep the results carries the
        // ones GetAsync handed it.
        entity.ResultArtifactsJson = material.ResultArtifactsJson;
        entity.UpdatedAtUtc = now;
        entity.Version = expectedVersion + 1;
        AddHistory(context, entity, previousState, session.State, now);
        // CompleteAsync claims a callback by moving the session to Importing.
        // Expiry or failure without a callback must not claim consumption.
        if (session.State == GlassRepairEstimateSessionState.Importing
            && entity.CallbackConsumedAtUtc is null)
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
    /// A replayed operation key must name the same launch: the same Case, the
    /// same Pegasus user, the same credential generation and the same
    /// account key. Anything else under that key is a collision, and
    /// returning the recorded session would hand one launch's provider material
    /// to another — a rotated credential or a different account is a different
    /// launch even when the Case and user match. The protected provider state
    /// is deliberately not compared: it is opaque here and a genuine replay
    /// carries fresh cookies.
    /// </summary>
    private static GlassRepairEstimateSessionMaterial RequireSameLaunch(
        GlassRepairEstimateSessionEntity replay,
        GlassRepairEstimateSession session,
        string accountKey,
        string operationKey) =>
        replay.CaseId == session.CaseId
        && replay.UserId == session.PegasusUserId
        && replay.CredentialGeneration == session.CredentialGeneration
        && string.Equals(replay.NormalizedAccountKey, accountKey, StringComparison.Ordinal)
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
            ToSession(entity),
            entity.ProtectedSession,
            entity.CallbackDigest,
            entity.ResultArtifactsJson);

    /// <summary>
    /// The session holding the account, read off the same column the unique
    /// index guards. A launch asks this first so an ordinary refusal is a
    /// read, and the index decides only a genuine race.
    /// </summary>
    public async Task<GlassRepairEstimateSession?> FindLiveForAccountAsync(
        string normalizedExternalAccountKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedExternalAccountKey);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ActiveAccountKey == normalizedExternalAccountKey, cancellationToken);
        return entity is null ? null : ToSession(entity);
    }

    /// <summary>
    /// The Engineer's session that holds an account now, wherever it was
    /// launched, so every Case they open can say where it is.
    /// </summary>
    public async Task<GlassRepairEstimateSession?> GetLiveForUserAsync(
        Guid pegasusUserId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .AsNoTracking()
            .Where(item => item.UserId == pegasusUserId && item.ActiveAccountKey != null)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToSession(entity);
    }

    private static GlassRepairEstimateSession ToSession(GlassRepairEstimateSessionEntity entity) =>
        new(
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
            entity.LastError,
            entity.CallbackConsumedAtUtc);

    public async Task<GlassRepairEstimateSession> CloseAsync(
        GlassRepairEstimateCloseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var entity = await context.Set<GlassRepairEstimateSessionEntity>()
            .SingleAsync(item => item.Id == request.SessionId, cancellationToken);
        if (entity.Version != request.ExpectedVersion)
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Version, request.SessionId,
                "The Glass's session changed before it could be closed.");
        }
        GlassRepairEstimateSessionPolicy.ValidateClosure(request, ToSession(entity));
        var now = timeProvider.GetUtcNow();
        var previousState = entity.State;
        entity.State = GlassRepairEstimateSessionState.Cancelled;
        entity.ActiveAccountKey = null;
        entity.LastError = null;
        entity.Version++;
        entity.UpdatedAtUtc = now;
        AddHistory(context, entity, previousState, entity.State, now, request.Actor, request.Reason.Trim());
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GlassRepairEstimateSessionConflictException(
                GlassRepairEstimateSessionConflict.Version, request.SessionId,
                "The Glass's session changed before it could be closed.");
        }
        return ToSession(entity);
    }

    private static bool OccupiesAccount(GlassRepairEstimateSessionState state) =>
        GlassRepairEstimateSessionPolicy.OccupiesAccount(state);

    private static void AddHistory(
        PegasusDbContext context, GlassRepairEstimateSessionEntity entity,
        GlassRepairEstimateSessionState? before, GlassRepairEstimateSessionState after,
        DateTimeOffset now, ActionActor? actor = null, string? reason = null)
    {
        context.ActionHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            AggregateType = "glass-repair-estimate-session",
            AggregateId = entity.Id.ToString("D"),
            EventKind = reason is null ? "glass_session_transition" : "glass_session_closed",
            ActorKind = actor?.Kind.ToString() ?? ActorKind.SystemWorker.ToString(),
            ActorSubjectId = actor?.SubjectId ?? "glass-repair-estimate-gateway",
            ActorRolesJson = JsonSerializer.Serialize(actor?.Roles.OrderBy(role => role).ToArray() ?? []),
            OccurredAtUtc = now,
            Outcome = "Succeeded",
            CorrelationId = entity.OperationKey,
            Reason = reason ?? "Glass's session checkpoint",
            BeforeJson = JsonSerializer.Serialize(new { State = before?.ToString() }),
            AfterJson = JsonSerializer.Serialize(new { State = after.ToString(), entity.Version, entity.LastError }),
            PolicyVersion = "glass-session-v1"
        });
    }

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
