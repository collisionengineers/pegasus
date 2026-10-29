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
/// record. The opaque token is returned only to its holder: persistence
/// retains it, as the Case lease does, so the holder's own claim resumes it and
/// a replayed claim returns it (R518, R519), and proves every presented token
/// against its retained digest.
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
    /// An authorised editor explicitly asks for a live scope. Their own resumes
    /// with the same token and nothing recorded (R518); a colleague's is taken
    /// over, its old token invalidated atomically and the takeover recorded,
    /// where <see cref="CaseEditAuthority.CanTakeOver"/> allows it: the
    /// Automation Actor never takes over an Automation holder (R517). Without
    /// it, any live scope refuses the claim (<see cref="EditScopeAuthority.RequireClaim"/>).
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
/// What <see cref="EditScopeAuthority.RequireClaim"/> lets a claim do: claim a scope nobody holds,
/// resume the caller's own live scope, or take over a colleague's.
/// </summary>
public enum EditScopeClaim
{
    New = 0,
    Resume = 1,
    TakeOver = 2
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
    /// The Case rule for both (R516, 29 October 2026): a scope is claimed at the record's current
    /// version, and a claim or a write under the scope is refused only when it expects a version
    /// newer than the one the record or the scope stands at
    /// (<see cref="CaseEditAuthority.RequireVersionUnderLease"/>). An older expectation is not the
    /// scope's refusal; a record store's own version check on its write is its own.
    /// </summary>
    public static void RequireVersion(
        EditScopeKind scopeKind,
        Guid recordId,
        long currentVersion,
        long expectedVersion)
    {
        if (expectedVersion > currentVersion)
        {
            throw new EditScopeVersionConflictException(
                scopeKind, recordId, expectedVersion, currentVersion);
        }
    }

    /// <summary>
    /// What a claim on a record scope does, by the Case rules. A scope that is not held is claimed
    /// afresh. A live scope refuses every claim that does not ask to take it over, the holder's own
    /// one-off claim included, so a one-off command never ends the holder's open window. Asked, the
    /// staff holder's own live scope resumes with the same token and nothing recorded
    /// (<see cref="CaseEditAuthority.CanResume"/>; R518, 29 October 2026), and a colleague's is taken
    /// over where <see cref="CaseEditAuthority.CanTakeOver"/> allows it: the Automation Actor never
    /// takes over an Automation holder, itself included, and a holder whose kind was never recorded
    /// is taken over by nobody (R517, 29 October 2026).
    /// </summary>
    public static EditScopeClaim RequireClaim(
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        bool takeOver,
        ActorKind? retainedHolderKind,
        string? retainedHolder,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!IsHeld(expiresAtUtc, nowUtc))
        {
            return EditScopeClaim.New;
        }

        if (takeOver)
        {
            if (CaseEditAuthority.CanResume(retainedHolderKind, retainedHolder, expiresAtUtc, actor, nowUtc))
            {
                return EditScopeClaim.Resume;
            }

            if (!IsHolder(retainedHolderKind, retainedHolder, actor)
                && CaseEditAuthority.CanTakeOver(retainedHolderKind, actor))
            {
                return EditScopeClaim.TakeOver;
            }
        }

        throw new EditScopeConflictException(scopeKind, recordId);
    }

    /// <summary>
    /// A claim replayed with the operation key and details of the claim that issued the retained
    /// scope returns that scope's token, for a record scope as for a Case (R519, 29 October 2026),
    /// while the scope is still live: a lapsed one is expired, as a lapsed Case lease's replay is.
    /// The retained scope is the replayed claimant's, since that claim made it.
    /// </summary>
    public static void RequireReplay(
        EditScopeKind scopeKind,
        Guid recordId,
        ActionActor actor,
        ActorKind? retainedHolderKind,
        string? retainedHolder,
        DateTimeOffset? expiresAtUtc,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!IsHeld(expiresAtUtc, nowUtc))
        {
            throw new EditScopeExpiredException(scopeKind, recordId);
        }

        if (!IsHolder(retainedHolderKind, retainedHolder, actor))
        {
            throw new EditScopeConflictException(scopeKind, recordId);
        }
    }

    /// <summary>
    /// Refuses a heartbeat, release or write that does not present the scope its actor holds, by
    /// the Case rule (<see cref="CaseEditAuthority.RequireLease"/>). No retained scope, or a
    /// presented token that is missing or not an issued token's length, is expired. A live scope
    /// held by another actor, or whose retained hash the presented token does not match, is a
    /// conflict. A holder whose scope lapsed but whose token still matches carries on, staff or
    /// the Automation Actor alike: nobody claimed the record since (R515, 29 October 2026); a
    /// lapsed scope that is not the presenter's, or whose token does not match, is expired.
    /// Infrastructure supplies the retained material and the fixed-time comparison, which is asked
    /// only once the holder has been matched; the refusal order is business policy and lives here.
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
        if (string.IsNullOrWhiteSpace(retainedHolder)
            || string.IsNullOrWhiteSpace(presentedLeaseToken)
            || presentedLeaseToken.Length != CaseEditAuthority.LeaseTokenLength)
        {
            throw new EditScopeExpiredException(scopeKind, recordId);
        }

        var proven = IsHolder(retainedHolderKind, retainedHolder, actor)
            && presentedTokenMatchesRetainedHash(presentedLeaseToken);
        if (proven)
        {
            return;
        }

        if (IsHeld(expiresAtUtc, nowUtc))
        {
            throw new EditScopeConflictException(scopeKind, recordId);
        }

        throw new EditScopeExpiredException(scopeKind, recordId);
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
