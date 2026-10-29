using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The mutable Triage and Image Intake records which use the common edit
/// ownership boundary. A scope is always one record, never a page or a
/// collection.
/// </summary>
public enum EditScopeKind
{
    Triage = 0,
    ImageIntake = 1
}

/// <summary>
/// A short-lived authorised claim on one mutable Triage or Image Intake
/// record. The opaque token is returned only to the claimant; persistence
/// retains only its digest.
/// </summary>
public sealed record EditScopeLease(
    EditScopeKind ScopeKind,
    Guid RecordId,
    string Token,
    string Holder,
    long RecordVersion,
    DateTimeOffset ExpiresAtUtc)
{
    public long Generation { get; init; }
}

/// <summary>
/// The live ownership state an authorised reader may use to explain why an
/// existing record cannot be edited. The retained subject remains an internal
/// identity; presentation resolves it through the staff-account query before
/// showing a name.
/// </summary>
public sealed record EditScopeSnapshot(
    EditScopeKind ScopeKind,
    Guid RecordId,
    ActorKind? HolderKind,
    string Holder,
    long RecordVersion,
    DateTimeOffset ExpiresAtUtc);

public sealed record ClaimEditScopeRequest(
    EditScopeKind ScopeKind,
    Guid RecordId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey)
{
    /// <summary>
    /// An authorised editor explicitly replaces a live scope. The old token is
    /// invalidated atomically. Replacing a colleague's scope is a takeover and
    /// is recorded; replacing the caller's own is not. Without it, any live
    /// scope refuses the claim.
    /// </summary>
    public bool TakeOver { get; init; }
}

public sealed record HeartbeatEditScopeRequest(
    EditScopeKind ScopeKind,
    Guid RecordId,
    ActionActor Actor,
    string LeaseToken);

public sealed record ReleaseEditScopeRequest(
    EditScopeKind ScopeKind,
    Guid RecordId,
    ActionActor Actor,
    string OperationKey,
    string LeaseToken);

public sealed class EditScopeVersionConflictException(
    EditScopeKind scopeKind,
    Guid recordId,
    long expectedVersion,
    long actualVersion)
    : InvalidOperationException(
        $"{scopeKind} '{recordId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public EditScopeKind ScopeKind { get; } = scopeKind;
    public Guid RecordId { get; } = recordId;
    public long ExpectedVersion { get; } = expectedVersion;
    public long ActualVersion { get; } = actualVersion;
}

public sealed class EditScopeConflictException(EditScopeKind scopeKind, Guid recordId)
    : InvalidOperationException($"{scopeKind} '{recordId}' is currently being edited by another actor.")
{
    public EditScopeKind ScopeKind { get; } = scopeKind;
    public Guid RecordId { get; } = recordId;
}

public sealed class EditScopeExpiredException(EditScopeKind scopeKind, Guid recordId)
    : InvalidOperationException($"The edit lease for {scopeKind} '{recordId}' is no longer valid.")
{
    public EditScopeKind ScopeKind { get; } = scopeKind;
    public Guid RecordId { get; } = recordId;
}

/// <summary>
/// The policy shared by Triage and Image intake edit scopes. Case persistence
/// remains its own owner.
/// </summary>
public static class EditScopeAuthority
{
    public static void RequireActor(EditScopeKind scopeKind, ActionActor actor)
    {
        var right = scopeKind switch
        {
            EditScopeKind.Triage or EditScopeKind.ImageIntake => StaffAccessRight.PerformCasework,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeKind))
        };
        StaffAuthorization.Require(actor, right);
    }

    public static readonly TimeSpan Duration = CaseEditAuthority.LeaseDuration;

    public static readonly TimeSpan HeartbeatInterval = CaseEditAuthority.HeartbeatInterval;

    public static bool IsHeld(DateTimeOffset? expiresAtUtc, DateTimeOffset nowUtc) =>
        CaseEditAuthority.IsHeld(expiresAtUtc, nowUtc);

    public static bool IsHolder(
        ActorKind? retainedHolderKind,
        string? retainedHolder,
        ActionActor actor) =>
        CaseEditAuthority.IsHolder(retainedHolderKind, retainedHolder, actor);

    /// <summary>
    /// A scope is claimed, and a write made under it, only at the version the record or the
    /// scope stands at; any difference is refused.
    /// </summary>
    public static void RequireVersion(
        EditScopeKind scopeKind,
        Guid recordId,
        long currentVersion,
        long expectedVersion)
    {
        if (currentVersion != expectedVersion)
        {
            throw new EditScopeVersionConflictException(
                scopeKind, recordId, expectedVersion, currentVersion);
        }
    }

    /// <summary>
    /// Refuses a heartbeat, release or write that does not present the live scope its actor
    /// holds. No live scope, or a presented token that is missing or not an issued token's
    /// length, is expired; a live scope held by another actor, or whose retained hash the
    /// presented token does not match, is a conflict. Infrastructure supplies the retained
    /// material and the fixed-time comparison, which is asked only once every other check has
    /// passed; the refusal order is business policy and lives here.
    /// </summary>
    public static void RequireScope(
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        string? presentedLeaseToken,
        ActorKind? retainedHolderKind,
        string? retainedHolder,
        DateTimeOffset? expiresAtUtc,
        Func<string, bool> presentedTokenMatchesRetainedHash,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(presentedTokenMatchesRetainedHash);
        if (!IsHeld(expiresAtUtc, nowUtc)
            || string.IsNullOrWhiteSpace(presentedLeaseToken)
            || presentedLeaseToken.Length != CaseEditAuthority.LeaseTokenLength)
        {
            throw new EditScopeExpiredException(scopeKind, recordId);
        }

        if (!IsHolder(retainedHolderKind, retainedHolder, actor)
            || !presentedTokenMatchesRetainedHash(presentedLeaseToken))
        {
            throw new EditScopeConflictException(scopeKind, recordId);
        }
    }
}

public interface IEditScopeLeases
{
    Task<EditScopeSnapshot?> GetActiveAsync(
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        CancellationToken cancellationToken);

    Task<EditScopeLease> ClaimAsync(
        ClaimEditScopeRequest request,
        CancellationToken cancellationToken);

    Task<EditScopeLease> HeartbeatAsync(
        HeartbeatEditScopeRequest request,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        ReleaseEditScopeRequest request,
        CancellationToken cancellationToken);
}
