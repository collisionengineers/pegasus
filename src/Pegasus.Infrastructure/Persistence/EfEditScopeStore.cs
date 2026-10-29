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
        EditScopeAuthority.RequireVersion(
            request.ScopeKind, request.RecordId, currentVersion.Value, request.ExpectedVersion);
        // A live scope refuses every claim that does not explicitly replace it, so a one-off
        // command never rotates the holder's open window. The record page's Edit replaces the
        // viewer's own scope without a takeover; only a colleague's scope is a takeover, and only
        // that is history.
        var isLive = scope is not null && EditScopeAuthority.IsHeld(scope.ExpiresAtUtc, now);
        if (isLive && !request.TakeOver)
        {
            throw new EditScopeConflictException(request.ScopeKind, request.RecordId);
        }
        var previousHolderKind = ParseHolderKind(scope?.HolderKind);
        var previousHolder = isLive
            && !EditScopeAuthority.IsHolder(previousHolderKind, scope!.Holder, request.Actor)
                ? scope!.Holder
                : null;

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var generation = checked((scope?.Generation ?? 0) + 1);
        if (scope is null)
        {
            scope = new EditScopeEntity
            {
                ScopeKind = ToCode(request.ScopeKind),
                RecordId = request.RecordId,
                HolderKind = request.Actor.Kind.ToString(),
                Holder = request.Actor.SubjectId,
                TokenHash = Hash(token)
            };
            context.Set<EditScopeEntity>().Add(scope);
        }

        scope.HolderKind = request.Actor.Kind.ToString();
        scope.Holder = request.Actor.SubjectId;
        scope.TokenHash = Hash(token);
        scope.ExpectedVersion = request.ExpectedVersion;
        scope.Generation = generation;
        scope.ExpiresAtUtc = now.Add(EditScopeAuthority.Duration);
        if (previousHolder is not null)
        {
            await AddTakeoverHistoryAsync(
                context, request, previousHolderKind, previousHolder, now, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            request.ScopeKind,
            request.RecordId,
            token,
            request.Actor.SubjectId,
            request.ExpectedVersion,
            scope.ExpiresAtUtc)
        {
            Generation = generation
        };
    }

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
            presented => CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(scope!.TokenHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(presented))),
            now);

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
        CancellationToken cancellationToken)
    {
        var holder = await CaseEditAuthorityHolder.ResolveAsync(
            previousHolderKind,
            previousHolder,
            (staffId, token) => context.Users.AsNoTracking()
                .Where(item => item.Id == staffId)
                .Select(item => item.UserName)
                .SingleOrDefaultAsync(token),
            cancellationToken);
        return holder.TakeOverHistoryReason;
    }

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
