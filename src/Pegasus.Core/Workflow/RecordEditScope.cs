using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The mutable non-Case records which use the common edit ownership
/// boundary. A scope is always one record, never a page or a collection.
/// </summary>
public enum EditScopeKind
{
    Triage,
    ImageIntake,
    Contact,
    StaffAccount,
    ValuationPreset,
    ApprovedMailbox,
    ApprovedOutlookCategory,
    NamedConfiguration,
    LabourRateCard
}

/// <summary>
/// A short-lived authorised claim on one mutable non-Case record. The opaque token
/// is returned only to the claimant; persistence retains only its digest.
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
    /// Set when the holder has decided to continue editing here rather than in
    /// the window that still holds the scope. It replaces the holder's own live
    /// scope with a new token; it never takes a scope from another actor.
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

public class EditScopeConflictException : InvalidOperationException
{
    public EditScopeConflictException(EditScopeKind scopeKind, Guid recordId)
        : this(scopeKind, recordId, $"{scopeKind} '{recordId}' is currently being edited by another actor.")
    {
    }

    /// <remarks>
    /// The one derived refusal below is still a conflict, so a caller that has
    /// no separate answer for it keeps the ordinary conflict behaviour instead
    /// of failing open or failing loudly.
    /// </remarks>
    protected EditScopeConflictException(EditScopeKind scopeKind, Guid recordId, string message)
        : base(message)
    {
        ScopeKind = scopeKind;
        RecordId = recordId;
    }

    public EditScopeKind ScopeKind { get; }
    public Guid RecordId { get; }
}

/// <summary>
/// The requesting actor already holds this scope in another live window. It is
/// not a refusal on another actor's behalf: the holder may continue here by
/// claiming again with <see cref="ClaimEditScopeRequest.TakeOver"/>, which
/// rotates the token so the abandoned window can no longer save.
/// </summary>
public sealed class EditScopeHeldElsewhereException(EditScopeKind scopeKind, Guid recordId)
    : EditScopeConflictException(
        scopeKind,
        recordId,
        $"{scopeKind} '{recordId}' is already being edited by the same actor in another window.");

public sealed class EditScopeExpiredException(EditScopeKind scopeKind, Guid recordId)
    : InvalidOperationException($"The edit lease for {scopeKind} '{recordId}' is no longer valid.")
{
    public EditScopeKind ScopeKind { get; } = scopeKind;
    public Guid RecordId { get; } = recordId;
}

/// <summary>
/// The policy shared by non-Case record scopes. Its duration and heartbeat
/// deliberately use the Case editor values: staff see one ownership model
/// across the application, while Case persistence remains its own owner.
/// </summary>
public static class EditScopeAuthority
{
    public static void RequireActor(EditScopeKind scopeKind, ActionActor actor)
    {
        var right = scopeKind switch
        {
            EditScopeKind.Triage or EditScopeKind.ImageIntake => StaffAccessRight.PerformCasework,
            EditScopeKind.Contact => StaffAccessRight.ManageOrganizationsAndPrincipals,
            EditScopeKind.StaffAccount => StaffAccessRight.ManageStaffAccounts,
            EditScopeKind.ApprovedMailbox => StaffAccessRight.ManageApprovedMailboxes,
            EditScopeKind.ApprovedOutlookCategory => StaffAccessRight.ManageApprovedOutlookCategories,
            EditScopeKind.ValuationPreset or EditScopeKind.NamedConfiguration or EditScopeKind.LabourRateCard
                => StaffAccessRight.ManageWorkflowConfiguration,
            _ => throw new ArgumentOutOfRangeException(nameof(scopeKind))
        };
        StaffAuthorization.Require(actor, right);
    }

    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan HeartbeatInterval = CaseEditAuthority.HeartbeatInterval;

    public static bool IsHeld(DateTimeOffset? expiresAtUtc, DateTimeOffset nowUtc) =>
        CaseEditAuthority.IsHeld(expiresAtUtc, nowUtc);

    public static bool IsHolder(
        ActorKind? retainedHolderKind,
        string? retainedHolder,
        ActionActor actor) =>
        CaseEditAuthority.IsHolder(retainedHolderKind, retainedHolder, actor);

    /// <summary>
    /// How long a still-unexpired scope must have gone unbeaten before its own
    /// holder may silently replace it. Three missed renewals are the shortest
    /// proof that the window which claimed it is gone: a hidden browser tab
    /// throttles its timers, so one missed renewal is routine and even two can
    /// occur without the window actually having left. A holder who is
    /// genuinely still editing elsewhere keeps the scope until they choose to
    /// take it over.
    /// </summary>
    public static readonly TimeSpan StaleAfter = HeartbeatInterval * 3;

    /// <summary>
    /// When the scope was last claimed or renewed. Persistence retains only the
    /// expiry, and every renewal sets it to the moment of renewal plus
    /// <see cref="Duration"/>, so the expiry carries the last heartbeat.
    /// </summary>
    public static DateTimeOffset LastHeartbeatUtc(DateTimeOffset expiresAtUtc) =>
        expiresAtUtc - Duration;

    public static bool IsStale(DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc) =>
        nowUtc - LastHeartbeatUtc(expiresAtUtc) > StaleAfter;
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

/// <summary>
/// Account administration invokes this when a staff session is revoked or an
/// account is disabled, so a dead session cannot retain an edit scope until
/// ordinary expiry.
/// </summary>
public interface IEditScopeRevocations
{
    Task ClearForActorAsync(ActionActor actor, CancellationToken cancellationToken);
}
