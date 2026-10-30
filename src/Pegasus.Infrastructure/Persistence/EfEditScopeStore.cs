using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one persistence owner for Triage and Image Intake edit scopes. Individual record
/// stores call <see cref="RequireAsync"/> and <see cref="Complete"/> inside
/// their existing mutation transaction; that makes the version, ownership and
/// mutation decision inseparable.
/// </summary>
public sealed class EfEditScopeStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IEditScopeLeases
{
    public async Task<EditScopeSnapshot?> GetActiveAsync(
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ValidateScope(scopeKind, recordId, actor);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var scope = await FindAsync(context, scopeKind, recordId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (scope is null || !EditScopeAuthority.IsHeld(scope.ExpiresAtUtc, now))
        {
            return null;
        }

        return new(
            scopeKind,
            recordId,
            ParseHolderKind(scope.HolderKind),
            scope.Holder,
            scope.ExpectedVersion,
            scope.ExpiresAtUtc);
    }

    /// <summary>
    /// Claims, resumes or takes over the record's scope as <see cref="EditScopeAuthority.RequireClaim"/>
    /// decides, at the record's current version (R516). A claim replayed with the operation key and
    /// details of the claim that issued the retained scope returns its token and writes nothing
    /// (R519); the claim that last issued or resumed the scope is the one a replay matches.
    /// </summary>
    public async Task<EditScopeLease> ClaimAsync(
        ClaimEditScopeRequest request,
        CancellationToken cancellationToken)
    {
        ValidateClaim(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var scope = await FindForUpdateAsync(context, request.ScopeKind, request.RecordId, cancellationToken);
        var currentVersion = await GetCurrentVersionAsync(
            context, request.ScopeKind, request.RecordId, cancellationToken);
        if (currentVersion is null)
        {
            throw new KeyNotFoundException($"{request.ScopeKind} '{request.RecordId}' was not found.");
        }

        var requestHash = ClaimRequestHash(request);
        var holderKind = ParseHolderKind(scope?.HolderKind);
        if (scope is not null && string.Equals(scope.ClaimRequestHash, requestHash, StringComparison.Ordinal))
        {
            EditScopeAuthority.RequireReplay(
                request.ScopeKind, request.RecordId, request.Actor, holderKind, scope.Holder,
                scope.ExpiresAtUtc, now);
            var replayed = RetainedToken(scope)
                ?? throw new EditScopeExpiredException(request.ScopeKind, request.RecordId);
            return Lease(scope, request.ScopeKind, replayed);
        }

        EditScopeAuthority.RequireVersion(
            request.ScopeKind, request.RecordId, currentVersion.Value, request.ExpectedVersion);
        var claim = EditScopeAuthority.RequireClaim(
            request.ScopeKind,
            request.RecordId,
            request.Actor,
            request.TakeOver,
            holderKind,
            scope?.Holder,
            scope?.ExpiresAtUtc,
            now);
        // Only a scope taken from a colleague is history; the holder resuming their own is not.
        if (claim == EditScopeClaim.TakeOver)
        {
            await AddTakeoverHistoryAsync(
                context, request, holderKind, scope!.Holder, now, cancellationToken);
        }

        // A resumed scope keeps its token and generation. A retained token its hash cannot prove
        // is never handed back, so that resume issues a new one, as a claim does.
        var token = claim == EditScopeClaim.Resume ? RetainedToken(scope!) : null;
        if (scope is null)
        {
            scope = new EditScopeEntity
            {
                ScopeKind = ToCode(request.ScopeKind),
                RecordId = request.RecordId,
                HolderKind = request.Actor.Kind.ToString(),
                Holder = request.Actor.SubjectId,
                TokenHash = string.Empty
            };
            context.Set<EditScopeEntity>().Add(scope);
        }

        if (token is null)
        {
            token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            scope.Generation = checked(scope.Generation + 1);
        }

        scope.HolderKind = request.Actor.Kind.ToString();
        scope.Holder = request.Actor.SubjectId;
        scope.Token = token;
        scope.TokenHash = Hash(token);
        scope.ClaimRequestHash = requestHash;
        scope.ExpectedVersion = currentVersion.Value;
        scope.ExpiresAtUtc = now.Add(EditScopeAuthority.Duration);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Lease(scope, request.ScopeKind, token);
    }

    /// <summary>
    /// Extends the holder's scope, and picks up a holder's lapsed scope nobody claimed since (R515),
    /// as a Case heartbeat does.
    /// </summary>
    public async Task<EditScopeLease> HeartbeatAsync(
        HeartbeatEditScopeRequest request,
        CancellationToken cancellationToken)
    {
        ValidateHeartbeat(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var scope = await FindForUpdateAsync(context, request.ScopeKind, request.RecordId, cancellationToken);
        Require(scope, request.ScopeKind, request.RecordId, request.Actor, request.LeaseToken, now);
        scope!.ExpiresAtUtc = now.Add(EditScopeAuthority.Duration);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            request.ScopeKind,
            request.RecordId,
            request.LeaseToken,
            request.Actor.SubjectId,
            scope.ExpectedVersion,
            scope.ExpiresAtUtc)
        {
            Generation = scope.Generation
        };
    }

    public async Task ReleaseAsync(
        ReleaseEditScopeRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRelease(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var scope = await FindForUpdateAsync(context, request.ScopeKind, request.RecordId, cancellationToken);
        Require(scope, request.ScopeKind, request.RecordId, request.Actor, request.LeaseToken,
            timeProvider.GetUtcNow());
        context.Remove(scope!);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Lets an account-administration transaction clear scopes before its own
    /// <c>SaveChanges</c>, so disabling or revoking a staff account cannot
    /// leave a scope committed after the account change.
    /// </summary>
    internal static async Task ClearForActorAsync(
        PegasusDbContext context,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actor);
        var scopes = await FindForActorAsync(context, actor, cancellationToken);
        context.RemoveRange(scopes);
    }

    /// <summary>Clears every scope held by one holder kind, inside the caller's transaction.</summary>
    internal static async Task<int> ClearForHolderKindAsync(
        PegasusDbContext context,
        ActorKind holderKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var kind = holderKind.ToString();
        var scopes = await context.Set<EditScopeEntity>()
            .Where(item => item.HolderKind == kind)
            .ToListAsync(cancellationToken);
        context.RemoveRange(scopes);
        return scopes.Count;
    }

    /// <summary>
    /// The scope a write is made under, proven inside the write's transaction: a version newer
    /// than the record's or the scope's is refused (R516), and the presented token must be the
    /// holder's, live or lapsed with nobody having claimed the record since (R515).
    /// </summary>
    internal static async Task RequireAsync(
        PegasusDbContext context,
        EditScopeKind scopeKind,
        Guid recordId,
        long currentVersion,
        long expectedVersion,
        ActionActor actor,
        string? leaseToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(actor);
        EditScopeAuthority.RequireVersion(scopeKind, recordId, currentVersion, expectedVersion);
        var scope = await FindForUpdateAsync(context, scopeKind, recordId, cancellationToken);
        Require(scope, scopeKind, recordId, actor, leaseToken, now);
        EditScopeAuthority.RequireVersion(scopeKind, recordId, scope!.ExpectedVersion, expectedVersion);
    }

    /// <summary>
    /// The scope a write was made under, once the write is made at <paramref name="recordVersion"/>.
    /// A staff scope ends. The Automation actor's scope stands at the new version until
    /// pegasus_edit_end releases it, so one token carries its multi-step work (operator,
    /// 8 October 2026).
    /// </summary>
    internal static void Complete(
        PegasusDbContext context, EditScopeKind scopeKind, Guid recordId, long recordVersion)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = context.Set<EditScopeEntity>().Local.SingleOrDefault(item =>
            item.ScopeKind == ToCode(scopeKind) && item.RecordId == recordId);
        if (scope is null)
        {
            throw new InvalidOperationException("The edit scope was not loaded by its mutation transaction.");
        }

        if (scope.HolderKind == nameof(ActorKind.Automation))
        {
            scope.ExpectedVersion = recordVersion;
            return;
        }

        context.Remove(scope);
    }

    private static async Task<EditScopeEntity?> FindAsync(
        PegasusDbContext context,
        EditScopeKind scopeKind,
        Guid recordId,
        CancellationToken cancellationToken) =>
        await context.Set<EditScopeEntity>().SingleOrDefaultAsync(item =>
            item.ScopeKind == ToCode(scopeKind) && item.RecordId == recordId,
            cancellationToken);

    /// <summary>
    /// The scope read <c>UPDLOCK, HOLDLOCK</c> for the rest of the transaction.
    /// A scope row has no concurrency token, so a claim, a heartbeat, a
    /// release and a write made under the scope each read it this way and
    /// queue on it; an absent scope locks its key.
    /// </summary>
    private static async Task<EditScopeEntity?> FindForUpdateAsync(
        PegasusDbContext context,
        EditScopeKind scopeKind,
        Guid recordId,
        CancellationToken cancellationToken) =>
        await context.Set<EditScopeEntity>()
            .FromSqlInterpolated($"""
                SELECT * FROM [EditScopes] WITH (UPDLOCK, HOLDLOCK)
                WHERE [ScopeKind] = {ToCode(scopeKind)} AND [RecordId] = {recordId}
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<long?> GetCurrentVersionAsync(
        PegasusDbContext context,
        EditScopeKind scopeKind,
        Guid recordId,
        CancellationToken cancellationToken) => scopeKind switch
    {
        EditScopeKind.Triage => await context.Triage.AsNoTracking()
            .Where(item => item.CaseId == recordId).Select(item => (long?)item.Version)
            .SingleOrDefaultAsync(cancellationToken),
        EditScopeKind.ImageIntake => await context.ImageIntakes.AsNoTracking()
            .Where(item => item.Id == recordId).Select(item => (long?)item.LifecycleVersion)
            .SingleOrDefaultAsync(cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(scopeKind))
    };

    /// <summary>
    /// Applies <see cref="EditScopeAuthority.RequireScope"/> to the retained scope, supplying the
    /// fixed-time comparison of the presented token against the retained hash.
    /// </summary>
    private static void Require(
        EditScopeEntity? scope,
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        string? leaseToken,
        DateTimeOffset now) =>
        EditScopeAuthority.RequireScope(
            scopeKind,
            recordId,
            actor,
            leaseToken,
            ParseHolderKind(scope?.HolderKind),
            scope?.Holder,
            scope?.ExpiresAtUtc,
            presented => TokenMatches(scope!.TokenHash, presented),
            now);

    private static EditScopeLease Lease(EditScopeEntity scope, EditScopeKind scopeKind, string token) =>
        new(scopeKind, scope.RecordId, token, scope.Holder, scope.ExpectedVersion, scope.ExpiresAtUtc)
        {
            Generation = scope.Generation
        };

    /// <summary>
    /// The retained plaintext token, only when it is the one the retained hash proves; anything
    /// else is unreadable and is never handed back.
    /// </summary>
    private static string? RetainedToken(EditScopeEntity scope) =>
        scope.Token is { Length: CaseEditAuthority.LeaseTokenLength } token
        && TokenMatches(scope.TokenHash, token)
            ? token
            : null;

    private static bool TokenMatches(string retainedHash, string presented) =>
        retainedHash.Length == 64
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(retainedHash),
            SHA256.HashData(Encoding.UTF8.GetBytes(presented)));

    /// <summary>
    /// The details a replayed claim must repeat: the record, the expected version, the actor, the
    /// operation key and whether it asked to take over.
    /// </summary>
    private static string ClaimRequestHash(ClaimEditScopeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            ScopeKind = ToCode(request.ScopeKind),
            request.RecordId,
            request.ExpectedVersion,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            OperationKey = request.OperationKey.Trim(),
            request.TakeOver
        }));

    private static ActorKind? ParseHolderKind(string? holderKind) =>
        Enum.TryParse<ActorKind>(holderKind, out var parsed) ? parsed : null;

    private static Task<List<EditScopeEntity>> FindForActorAsync(
        PegasusDbContext context,
        ActionActor actor,
        CancellationToken cancellationToken) => context.Set<EditScopeEntity>()
            .Where(item => item.HolderKind == actor.Kind.ToString()
                && item.Holder == actor.SubjectId)
            .ToListAsync(cancellationToken);

    private static void ValidateClaim(ClaimEditScopeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateScope(request.ScopeKind, request.RecordId, request.Actor);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
    }

    private static void ValidateHeartbeat(HeartbeatEditScopeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateScope(request.ScopeKind, request.RecordId, request.Actor);
        ValidateToken(request.LeaseToken);
    }

    private static void ValidateRelease(ReleaseEditScopeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateScope(request.ScopeKind, request.RecordId, request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ValidateToken(request.LeaseToken);
    }

    private static void ValidateScope(EditScopeKind scopeKind, Guid recordId, ActionActor actor)
    {
        if (!Enum.IsDefined(scopeKind) || recordId == Guid.Empty)
        {
            throw new ArgumentException("A supported edit scope and record identity are required.");
        }

        ArgumentNullException.ThrowIfNull(actor);
        if (string.IsNullOrWhiteSpace(actor.SubjectId))
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }
        EditScopeAuthority.RequireActor(scopeKind, actor);
    }

    private static void ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != CaseEditAuthority.LeaseTokenLength)
        {
            throw new ArgumentException("A valid edit lease token is required.", nameof(token));
        }
    }

    private static string ToCode(EditScopeKind scopeKind) => scopeKind.ToString();

    private static async Task AddTakeoverHistoryAsync(
        PegasusDbContext context,
        ClaimEditScopeRequest request,
        ActorKind? previousHolderKind,
        string previousHolder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reason = await TakeOverHistoryReasonAsync(
            context, previousHolderKind, previousHolder, cancellationToken);
        var fingerprint = Hash(
            $"{request.ScopeKind}|{request.RecordId:D}|{request.ExpectedVersion}|{request.Actor.SubjectId}|{request.OperationKey}");
        if (request.ScopeKind == EditScopeKind.Triage)
        {
            var triage = await context.Triage.SingleAsync(
                item => item.CaseId == request.RecordId, cancellationToken);
            context.TriageHistory.Add(new TriageHistoryEntity
            {
                Id = Guid.NewGuid(),
                TriageCaseId = triage.CaseId,
                EventType = "edit_lease_taken_over",
                Actor = request.Actor.SubjectId,
                ActorKind = request.Actor.Kind.ToString(),
                Reason = reason,
                OperationKey = request.OperationKey,
                RequestHash = fingerprint,
                OccurredAtUtc = now,
                BeforeVersion = triage.Version,
                AfterVersion = triage.Version,
                AfterState = triage.State,
                AfterAssigneeId = triage.AssigneeId,
                AfterLinkedInstructionCaseId = triage.LinkedInstructionCaseId
            });
        }
        else
        {
            var image = await context.ImageIntakes.SingleAsync(
                item => item.Id == request.RecordId, cancellationToken);
            context.ImageIntakeLifecycleEvents.Add(new ImageIntakeLifecycleEventEntity
            {
                Id = Guid.NewGuid(),
                ImageIntakeId = image.Id,
                EventType = "edit_lease_taken_over",
                ActorKind = request.Actor.Kind.ToString(),
                ActorSubjectId = request.Actor.SubjectId,
                ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
                Reason = reason,
                OperationKey = request.OperationKey,
                RequestFingerprint = fingerprint,
                OccurredAtUtc = now,
                BeforeVersion = image.LifecycleVersion,
                AfterVersion = image.LifecycleVersion
            });
        }
    }

    /// <summary>
    /// The takeover history line for a Case lease or an edit scope, naming the previous holder by
    /// <see cref="CaseEditAuthorityHolder.ResolveAsync"/> with the account read inside the
    /// caller's transaction.
    /// </summary>
    internal static async Task<string> TakeOverHistoryReasonAsync(
        PegasusDbContext context,
        ActorKind? previousHolderKind,
        string previousHolder,
        CancellationToken cancellationToken) =>
        (await ResolveHolderAsync(context, previousHolderKind, previousHolder, cancellationToken))
            .TakeOverHistoryReason;

    /// <summary>
    /// The one way a retained holder or requester is named for history: the Automation Actor by
    /// its kind, a member of staff by account name, otherwise unnamed (R520).
    /// </summary>
    internal static Task<CaseEditAuthorityHolder> ResolveHolderAsync(
        PegasusDbContext context,
        ActorKind? holderKind,
        string holder,
        CancellationToken cancellationToken) =>
        CaseEditAuthorityHolder.ResolveAsync(
            holderKind,
            holder,
            (staffId, token) => context.Users.AsNoTracking()
                .Where(item => item.Id == staffId)
                .Select(item => item.UserName)
                .SingleOrDefaultAsync(token),
            cancellationToken);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
