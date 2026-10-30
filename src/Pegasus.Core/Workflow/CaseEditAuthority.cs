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
    /// How long an edit lease lasts from the claim, resume or heartbeat that last extended it
    /// (FRD-14). The stores apply it; nothing else holds a second copy of it.
    /// </summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

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
    /// while any lease is live unless it asks to take that lease over.
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

    /// <summary>
    /// A takeover is a casework action. A staff-held lease may be taken over by a colleague or by
    /// the Automation Actor, which may do anything a staff member can (operator, 7 October 2026);
    /// a lease the Automation Actor holds, by a member of staff only (operator, 15 and 16 October
    /// 2026).
    /// </summary>
    public static bool CanTakeOver(ActorKind? retainedLeaseHolderKind, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework)
            && (retainedLeaseHolderKind == ActorKind.Staff
                || (retainedLeaseHolderKind == ActorKind.Automation && actor.Kind == ActorKind.Staff));
    }

    /// <summary>
    /// How long the holder of a Case lease is given to answer a request to edit before the
    /// requester may take the lease anyway (R552, 30 October 2026). Short enough that one
    /// Automation tool call, waiting inside the call, outlasts it under the MCP clients' default
    /// timeout of a minute.
    /// </summary>
    public static readonly TimeSpan RequestAnswerWindow = TimeSpan.FromSeconds(45);

    /// <summary>A request nobody has answered whose window is still open.</summary>
    public static bool IsRequestPending(
        DateTimeOffset requestedAtUtc,
        CaseEditRequestAnswer? answer,
        DateTimeOffset nowUtc) =>
        answer is null && requestedAtUtc + RequestAnswerWindow > nowUtc;

    /// <summary>A request nobody has answered whose window has closed.</summary>
    public static bool IsRequestUnanswered(
        DateTimeOffset requestedAtUtc,
        CaseEditRequestAnswer? answer,
        DateTimeOffset nowUtc) =>
        answer is null && !IsRequestPending(requestedAtUtc, answer, nowUtc);

    /// <summary>
    /// Whether a request still stands against the Case's lease: it was addressed to the holder
    /// who still holds the live lease. A request outlives neither its holder nor the lease.
    /// </summary>
    public static bool RequestStands(
        CaseEditRequestSnapshot? request,
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        DateTimeOffset? leaseExpiresAtUtc,
        DateTimeOffset nowUtc) =>
        request is not null
        && retainedLeaseHolderKind == ActorKind.Staff
        && string.Equals(request.Holder, retainedLeaseHolder, StringComparison.Ordinal)
        && IsHeld(leaseExpiresAtUtc, nowUtc);

    /// <summary>
    /// Who may ask the holder to hand the lease over (R550, 30 October 2026): any actor with the
    /// casework right, staff or the Automation Actor, while a member of staff other than the
    /// caller holds the live lease. Nobody asks the Automation Actor, which has no page to answer
    /// on: staff take its lease over at once (ADR-0075) and another Automation session is refused.
    /// </summary>
    public static bool CanRequest(
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        DateTimeOffset? leaseExpiresAtUtc,
        ActionActor actor,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework)
            && retainedLeaseHolderKind == ActorKind.Staff
            && IsHeld(leaseExpiresAtUtc, nowUtc)
            && !IsHolder(retainedLeaseHolderKind, retainedLeaseHolder, actor);
    }

    /// <summary>
    /// Whether the actor may answer a request: only the member of staff it was addressed to, who
    /// still holds the lease. The caller has already proven the lease with the holder's token,
    /// which lets a holder whose lease lapsed with nobody claiming since answer as they carry on.
    /// </summary>
    public static bool CanAnswer(
        CaseEditRequestSnapshot request,
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        return request.Answer is null
            && retainedLeaseHolderKind == ActorKind.Staff
            && string.Equals(request.Holder, retainedLeaseHolder, StringComparison.Ordinal)
            && IsHolder(retainedLeaseHolderKind, retainedLeaseHolder, actor);
    }

    /// <summary>
    /// Whether the actor is the one who made the request.
    /// </summary>
    public static bool IsRequester(CaseEditRequestSnapshot request, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actor);
        return request.RequesterKind == actor.Kind
            && string.Equals(request.Requester, actor.SubjectId, StringComparison.Ordinal);
    }

    /// <summary>
    /// A takeover as a request makes it (R552 and R554, 30 October 2026). A lease the Automation
    /// Actor holds is taken over at once by a member of staff, since nobody can be asked
    /// (ADR-0075). A lease a member of staff holds is taken over only by the member of staff who
    /// asked, once their request has gone unanswered through its window, while the holder they
    /// asked still holds the lease and has not answered; a declined request ends the matter. The
    /// Automation Actor asks and waits, and never takes a member of staff's lease.
    /// </summary>
    public static bool CanTakeOver(
        ActorKind? retainedLeaseHolderKind,
        string? retainedLeaseHolder,
        DateTimeOffset? leaseExpiresAtUtc,
        ActionActor actor,
        CaseEditRequestSnapshot? request,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework)
            || actor.Kind != ActorKind.Staff)
        {
            return false;
        }

        if (retainedLeaseHolderKind == ActorKind.Automation)
        {
            return true;
        }

        return request is not null
            && RequestStands(request, retainedLeaseHolderKind, retainedLeaseHolder, leaseExpiresAtUtc, nowUtc)
            && IsRequester(request, actor)
            && IsRequestUnanswered(request.RequestedAtUtc, request.Answer, nowUtc);
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
    /// Whether the lease a write was made under ends once the write is made. A staff lease ends,
    /// so the holder's next write presents the lease claimed again after it. The Automation
    /// Actor's lease stands until pegasus_edit_end releases it, so one token from
    /// pegasus_edit_begin carries every write of its multi-step work (operator, 8 October 2026).
    /// A write the staff member's session continues past keeps the lease too: a Glass's return
    /// landing its estimate under the staff member's own session
    /// (<paramref name="keepsLease"/>; operator, 6 October 2026). A write proves the lease first,
    /// so the retained holder is the writer.
    /// </summary>
    public static bool WriteEndsLease(ActorKind? retainedLeaseHolderKind, bool keepsLease) =>
        !keepsLease && retainedLeaseHolderKind != ActorKind.Automation;

    /// <summary>
    /// Refuses a mutation that does not present the lease its actor holds. The caller has
    /// already compared the presented token against the retained hash in fixed time;
    /// <paramref name="presentedTokenMatchesRetainedHash"/> is false when it does not match or when
    /// the retained hash cannot be read, so an unprovable token fails closed. A holder whose lease
    /// lapsed but whose token still matches carries on, staff or the Automation Actor alike: nobody
    /// claimed the Case since, and the caller extends the lease again.
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

        if (!isHolder || !presentedTokenMatchesRetainedHash)
        {
            throw new CaseEditLeaseExpiredException(caseId, caseVersion);
        }
    }

    /// <summary>
    /// A heartbeat answers to the same rule as a write: it extends the holder's lease, and picks
    /// up a holder's lapsed lease that nobody claimed since.
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
    /// <summary>
    /// The one word for the Automation Actor as a holder, wherever it is named: the holder chip
    /// and the takeover history line.
    /// </summary>
    public const string AutomationName = "AI";

    public static readonly CaseEditAuthorityHolder Unnamed = new(DisplayName: null);

    public static readonly CaseEditAuthorityHolder Automation =
        new(DisplayName: null, IsAutomation: true);

    /// <summary>
    /// The words for an <see cref="Unnamed"/> holder in the takeover history line: the holder
    /// chip's words for a holder it cannot name.
    /// </summary>
    public const string UnnamedName = "another member of staff";

    /// <summary>
    /// The one rule for naming a retained holder, for the holder chip and the takeover history
    /// line alike. The Automation Actor is named by its retained kind. Only a staff holder whose
    /// subject is a staff identifier has an account to name, read through
    /// <paramref name="accountName"/>; a holder whose kind was never recorded, whose subject is
    /// not a staff identifier, or whose account name cannot be read is <see cref="Unnamed"/>, never
    /// its identifier (R520, 29 October 2026).
    /// </summary>
    public static async Task<CaseEditAuthorityHolder> ResolveAsync(
        ActorKind? holderKind,
        string? holderSubjectId,
        Func<Guid, CancellationToken, Task<string?>> accountName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accountName);
        if (holderKind == ActorKind.Automation)
        {
            return Automation;
        }

        if (holderKind != ActorKind.Staff
            || !Guid.TryParse(holderSubjectId, out var staffId)
            || staffId == Guid.Empty)
        {
            return Unnamed;
        }

        var userName = await accountName(staffId, cancellationToken);
        return string.IsNullOrWhiteSpace(userName) ? Unnamed : new(userName);
    }

    /// <summary>
    /// The takeover history line naming this holder as the one the lease was taken over from:
    /// "Edit lease taken over from AI." for the Automation Actor (operator, 15 and 16 October
    /// 2026), the staff account name, or <see cref="UnnamedName"/> (R520, 29 October 2026).
    /// </summary>
    public string TakeOverHistoryReason => $"Edit lease taken over from {HistoryName}.";

    /// <summary>
    /// The history line for a lease the holder handed to this requester at their request (R553,
    /// 30 October 2026), under the same event type as a takeover and named by the same rule.
    /// </summary>
    public string HandOverHistoryReason => $"Edit lease handed over to {HistoryName} at their request.";

    private string HistoryName => IsAutomation
        ? AutomationName
        : string.IsNullOrWhiteSpace(DisplayName) ? UnnamedName : DisplayName;
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
        return await CaseEditAuthorityHolder.ResolveAsync(
            holderKind,
            holderSubjectId,
            async (staffId, token) =>
                (await _accounts.GetNamesAsync([staffId], token)).GetValueOrDefault(staffId),
            cancellationToken);
    }
}
