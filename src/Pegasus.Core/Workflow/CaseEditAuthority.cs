using Pegasus.Core.Identity;

namespace Pegasus.Core.Workflow;

/// <summary>
/// The single owner of the decision every staff case mutation is guarded by: the caller must
/// present the edit lease it holds, and no staff write may have landed since the editor loaded the
/// case. Only staff work ends a lease; system work moves the version under it without ending the
/// session. A missing, wrong-holder, or superseded mutation is refused without overwriting newer
/// work. The holder resumes their own lease from any window; an explicit authorised takeover by a
/// colleague rotates it before another edit. Infrastructure supplies the persisted material
/// and the fixed-time token comparison; the refusal order is business policy and lives here.
/// </summary>
public static class CaseEditAuthority
{
    /// <summary>
    /// Edit lease tokens are issued as 64 hexadecimal characters and retained in a column of that
    /// exact width, so a longer presented value can never round-trip and is refused as invalid.
    /// </summary>
    public const int LeaseTokenLength = 64;

    /// <summary>
    /// How often an open editor tells the server it is still there. Well inside the lease's own
    /// lifetime, so several beats can be lost — to a throttled background tab, a resumed machine,
    /// a slow request — before the lease lapses. The page renders this value; nothing else holds a
    /// second copy of it.
    /// </summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// True when a retained expiry is still in the future by server time. An abandoned lease
    /// expires without a sweeper, so every projection and guard asks this one question.
    /// </summary>
    public static bool IsHeld(DateTimeOffset? leaseExpiresAtUtc, DateTimeOffset nowUtc) =>
        leaseExpiresAtUtc is { } expiresAtUtc && expiresAtUtc > nowUtc;

    /// <summary>
    /// The holder is the staff member, not the window. A Case page its staff holder opens again
    /// — a second tab, or a return from another case — resumes the live lease as it stands: the
    /// same token, no rotation and no takeover history. Take over is only ever a colleague's
    /// action. The Automation Actor never resumes: each of its sessions claims, and fails closed
    /// while any lease is live.
    /// </summary>
    public static bool CanResume(
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        DateTimeOffset? leaseExpiresAtUtc,
        ActionActor actor,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return actor.Kind == ActorKind.Staff
            && IsHeld(leaseExpiresAtUtc, nowUtc)
            && IsHolder(retainedLeaseHolderKind, retainedLeaseHolder, actor);
    }

    /// <summary>A colleague takeover is a staff action on a staff-held lease.</summary>
    public static bool CanTakeOver(ActorKind? retainedLeaseHolderKind, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return retainedLeaseHolderKind == ActorKind.Staff && actor.Kind == ActorKind.Staff;
    }

    public static void RequireVersion(Guid caseId, long caseVersion, long expectedVersion)
    {
        if (caseVersion != expectedVersion)
        {
            throw new CaseVersionConflictException(caseId, expectedVersion, caseVersion);
        }
    }

    /// <summary>
    /// Whether the retained holder is this actor. A holder is an <see cref="ActionActor"/>
    /// identity — kind and subject together — so a staff account and the Automation Actor are
    /// never the same holder even when their subject text coincides, and a retained holder whose
    /// kind was never recorded is nobody's: it competes with every caller until it expires.
    /// </summary>
    public static bool IsHolder(
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return retainedLeaseHolderKind == actor.Kind
            && string.Equals(retainedLeaseHolder, actor.SubjectId, StringComparison.Ordinal);
    }

    /// <summary>
    /// The version rule for a write under a proven lease. Every staff write ends the lease it was
    /// made under, and a page receives a token only with the version it was issued at or a later
    /// one, so a presented token that still matches proves no staff write has landed since the page
    /// read its version: anything that moved the version since was system work, which never ends a
    /// member of staff's edit session (operator, 6 October 2026). The caller has already proven
    /// the lease; a version from the future is refused.
    /// </summary>
    public static void RequireVersionUnderLease(Guid caseId, long caseVersion, long expectedVersion)
    {
        if (expectedVersion > caseVersion)
        {
            throw new CaseVersionConflictException(caseId, expectedVersion, caseVersion);
        }
    }

    /// <summary>
    /// Refuses a mutation that does not present the lease its actor holds. The caller has
    /// already compared the presented token against the retained hash in fixed time;
    /// <paramref name="presentedTokenMatchesRetainedHash"/> is false when it does not match or when
    /// the retained hash cannot be read, so an unprovable token fails closed. A staff holder whose
    /// lease lapsed but whose token still matches carries on: nobody claimed the Case since, and the
    /// caller extends the lease again. The Automation Actor's lease ends at its expiry.
    /// </summary>
    public static void RequireLease(
        Guid caseId,
        long caseVersion,
        ActionActor actor,
        string? presentedLeaseToken,
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        bool hasRetainedLeaseTokenHash,
        DateTimeOffset? leaseExpiresAtUtc,
        bool presentedTokenMatchesRetainedHash,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (string.IsNullOrWhiteSpace(presentedLeaseToken)
            || !hasRetainedLeaseTokenHash
            || string.IsNullOrWhiteSpace(retainedLeaseHolder))
        {
            throw new CaseEditLeaseExpiredException(caseId, caseVersion);
        }

        var isHolder = IsHolder(retainedLeaseHolderKind, retainedLeaseHolder, actor);
        if (IsHeld(leaseExpiresAtUtc, nowUtc))
        {
            if (!isHolder || !presentedTokenMatchesRetainedHash)
            {
                throw new CaseEditLeaseConflictException(caseId, caseVersion);
            }

            return;
        }

        if (actor.Kind != ActorKind.Staff || !isHolder || !presentedTokenMatchesRetainedHash)
        {
            throw new CaseEditLeaseExpiredException(caseId, caseVersion);
        }
    }

    /// <summary>
    /// A heartbeat answers to the same rule as a write: it extends the holder's lease, and picks
    /// up a staff holder's lapsed lease that nobody claimed since.
    /// </summary>
    public static void RequireHeartbeat(
        Guid caseId,
        long caseVersion,
        ActionActor actor,
        string? presentedLeaseToken,
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        bool hasRetainedLeaseTokenHash,
        DateTimeOffset? leaseExpiresAtUtc,
        bool presentedTokenMatchesRetainedHash,
        DateTimeOffset nowUtc) =>
        RequireLease(
            caseId,
            caseVersion,
            actor,
            presentedLeaseToken,
            retainedLeaseHolderKind,
            retainedLeaseHolder,
            hasRetainedLeaseTokenHash,
            leaseExpiresAtUtc,
            presentedTokenMatchesRetainedHash,
            nowUtc);
}

/// <summary>
/// How the holder of a case's edit authority is disclosed to other authorised staff. A resolved
/// account is named; an unresolvable one is described without an identifier, because the retained
/// holder is a subject identifier and an identifier is never operator-facing. The Automation Actor
/// is disclosed as itself by its retained kind, never by the shape of its subject: ADR-0011
/// requires it to stay attributable without impersonating staff, so it is never described as a
/// member of staff.
/// </summary>
public sealed record CaseEditAuthorityHolder(string? DisplayName, bool IsAutomation = false)
{
    public static readonly CaseEditAuthorityHolder Unnamed = new(DisplayName: null);

    public static readonly CaseEditAuthorityHolder Automation =
        new(DisplayName: null, IsAutomation: true);
}

public interface IDescribeCaseEditAuthorityHolder
{
    Task<CaseEditAuthorityHolder> ExecuteAsync(
        ActorKind? holderKind,
        string holderSubjectId,
        ActionActor actor,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the retained holder to the staff account name other authorised staff may see, through
/// the same staff-account read the administration surface uses. Casework permission is enough:
/// the requirement gives every authorised editor sight of who holds a case, and nothing beyond the
/// account name is disclosed.
/// </summary>
public sealed class DescribeCaseEditAuthorityHolder(IStaffAccountQueries accounts)
    : IDescribeCaseEditAuthorityHolder
{
    private readonly IStaffAccountQueries _accounts =
        accounts ?? throw new ArgumentNullException(nameof(accounts));

    public async Task<CaseEditAuthorityHolder> ExecuteAsync(
        ActorKind? holderKind,
        string holderSubjectId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        if (holderKind == ActorKind.Automation)
        {
            return CaseEditAuthorityHolder.Automation;
        }

        // Only a staff holder has an account to name. A holder whose kind was never recorded, or
        // whose subject is not a staff identifier, is described without one.
        if (holderKind != ActorKind.Staff
            || !Guid.TryParse(holderSubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            return CaseEditAuthorityHolder.Unnamed;
        }

        var account = await _accounts.GetAsync(staffId, cancellationToken);
        return account is null || string.IsNullOrWhiteSpace(account.UserName)
            ? CaseEditAuthorityHolder.Unnamed
            : new(account.UserName);
    }
}
