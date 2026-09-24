using System.Data;
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
    TimeProvider timeProvider) : IEditScopeLeases, IEditScopeRevocations
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
            Enum.TryParse<ActorKind>(scope.HolderKind, out var holderKind)
                ? holderKind
                : null,
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
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var currentVersion = await GetCurrentVersionAsync(
            context, request.ScopeKind, request.RecordId, cancellationToken);
        if (currentVersion is null)
        {
            throw new KeyNotFoundException($"{request.ScopeKind} '{request.RecordId}' was not found.");
        }
        if (currentVersion.Value != request.ExpectedVersion)
        {
            throw new EditScopeVersionConflictException(
                request.ScopeKind, request.RecordId, request.ExpectedVersion, currentVersion.Value);
        }
        var scope = await FindAsync(context, request.ScopeKind, request.RecordId, cancellationToken);
        var previousHolder = scope is not null && EditScopeAuthority.IsHeld(scope.ExpiresAtUtc, now)
            ? scope.Holder
            : null;
        if (previousHolder is not null)
        {
            var isHolder = Enum.TryParse<ActorKind>(scope!.HolderKind, out var holderKind)
                && EditScopeAuthority.IsHolder(holderKind, scope.Holder, request.Actor);
            if (!isHolder && !request.TakeOver)
            {
                throw new EditScopeConflictException(request.ScopeKind, request.RecordId);
            }
            if (isHolder && !request.TakeOver && !EditScopeAuthority.IsStale(scope.ExpiresAtUtc, now))
            {
                throw new EditScopeHeldElsewhereException(request.ScopeKind, request.RecordId);
            }
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
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
        if (previousHolder is not null && request.TakeOver)
        {
            await AddTakeoverHistoryAsync(
                context, request, previousHolder, now, cancellationToken);
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
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var now = timeProvider.GetUtcNow();
        var scope = await FindAsync(context, request.ScopeKind, request.RecordId, cancellationToken);
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
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var scope = await FindAsync(context, request.ScopeKind, request.RecordId, cancellationToken);
        Require(scope, request.ScopeKind, request.RecordId, request.Actor, request.LeaseToken,
            timeProvider.GetUtcNow());
        context.Remove(scope!);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ClearForActorAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var scopes = await FindForActorAsync(context, actor, cancellationToken);
        if (scopes.Count == 0)
        {
            return;
        }

        context.RemoveRange(scopes);
        await context.SaveChangesAsync(cancellationToken);
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
        if (currentVersion != expectedVersion)
        {
            throw new EditScopeVersionConflictException(
                scopeKind, recordId, expectedVersion, currentVersion);
        }

        var scope = await FindAsync(context, scopeKind, recordId, cancellationToken);
        Require(scope, scopeKind, recordId, actor, leaseToken, now);
        if (scope!.ExpectedVersion != expectedVersion)
        {
            throw new EditScopeVersionConflictException(
                scopeKind, recordId, expectedVersion, scope.ExpectedVersion);
        }
    }

    internal static void Complete(PegasusDbContext context, EditScopeKind scopeKind, Guid recordId)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = context.Set<EditScopeEntity>().Local.SingleOrDefault(item =>
            item.ScopeKind == ToCode(scopeKind) && item.RecordId == recordId);
        if (scope is null)
        {
            throw new InvalidOperationException("The edit scope was not loaded by its mutation transaction.");
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

    private static void Require(
        EditScopeEntity? scope,
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        string? leaseToken,
        DateTimeOffset now)
    {
        if (scope is null || !EditScopeAuthority.IsHeld(scope.ExpiresAtUtc, now))
        {
            throw new EditScopeExpiredException(scopeKind, recordId);
        }

        RequireHolder(scope, scopeKind, recordId, actor, leaseToken);
    }

    private static void RequireHolder(
        EditScopeEntity? scope,
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        string? leaseToken)
    {
        if (scope is null
            || string.IsNullOrWhiteSpace(leaseToken)
            || leaseToken.Length != CaseEditAuthority.LeaseTokenLength)
        {
            throw new EditScopeExpiredException(scopeKind, recordId);
        }

        if (!Enum.TryParse<ActorKind>(scope.HolderKind, out var holderKind)
            || !EditScopeAuthority.IsHolder(holderKind, scope.Holder, actor)
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(scope.TokenHash),
                SHA256.HashData(Encoding.UTF8.GetBytes(leaseToken))))
        {
            throw new EditScopeConflictException(scopeKind, recordId);
        }
    }

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
        string previousHolder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var name = previousHolder;
        if (Guid.TryParse(previousHolder, out var staffId))
        {
            name = await context.Users.AsNoTracking()
                .Where(item => item.Id == staffId)
                .Select(item => item.UserName)
                .SingleOrDefaultAsync(cancellationToken) ?? previousHolder;
        }
        var reason = $"Edit lease taken over from {name}.";
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

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
