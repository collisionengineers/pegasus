using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The Glass's sessions of one Case the Automation's Glass's tools choose
/// from: the Case's newest session the Automation owns, and the staff
/// sessions that are <c>Active</c> or have had help turned on.
/// </summary>
public sealed record GlassSessionHelpCandidates(
    GlassRepairEstimateSession? Automation, IReadOnlyList<GlassRepairEstimateSession> Staff);

/// <summary>
/// The session the Automation's Glass's tools act in on a Case, and the
/// <c>glass.help.*</c> code that refuses acting in it: a session with no
/// code may be acted in, a session with a code is shown but not acted in,
/// and no session means none was chosen.
/// </summary>
public sealed record GlassSessionHelpTarget(GlassRepairEstimateSession? Session, string? Code)
{
    /// <summary>Whether the session is a member of staff's, which the Automation only helps in.</summary>
    public bool IsStaff => Session is { OwnerKind: ActorKind.Staff };
}

/// <summary>
/// A help act or read refused before anything was sent to Glass's, with its
/// <c>glass.help.*</c> code (<see cref="GlassSessionHelpPolicy"/>).
/// </summary>
public sealed class GlassSessionHelpRefusalException(string code)
    : InvalidOperationException(code)
{
    public string Code { get; } = code;
}

/// <summary>A help act as its history row recorded it: the staff session it was made in, and the act.</summary>
public sealed record GlassSessionHelpRecordedAct(Guid SessionId, GlassAutomationAct Act);

/// <summary>
/// Where the Automation may act in a member of staff's open Glass's session
/// (operator, 18 November 2026; R1170, R1182, R1184, R1185): only while its
/// owner has turned help on from the Case page and the session is
/// <c>Active</c>. The Automation finds the session by its Case, never saves,
/// exits or closes it, and never reads the member of staff's Glass's login.
/// </summary>
public static class GlassSessionHelpPolicy
{
    /// <summary>No session on the Case is open to the Automation.</summary>
    public const string NoSessionCode = "glass.help.no_session";

    /// <summary>A member of staff's session is <c>Active</c> but its owner has not turned help on.</summary>
    public const string OffCode = "glass.help.off";

    /// <summary>More than one session is open to the Automation; the caller names one by its id.</summary>
    public const string SeveralCode = "glass.help.several";

    /// <summary>The session is no longer <c>Active</c>: saved, closed or reopened elsewhere.</summary>
    public const string NotActiveCode = "glass.help.not_active";

    /// <summary>Work runs for the session, or another help act holds it.</summary>
    public const string BusyCode = "glass.help.busy";

    /// <summary>The session's Glass's window has not handed its key to Pegasus: the helper is missing or the window is closed.</summary>
    public const string NoWindowCode = "glass.help.no_window";

    /// <summary>Starting, finishing and closing are the Automation's own session's only; a member of staff's is theirs.</summary>
    public const string NotAutomationSessionCode = "glass.help.not_automation_session";

    /// <summary>Whether the session is a member of staff's with help turned on, whatever its state.</summary>
    public static bool HelpOn(GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.OwnerKind == ActorKind.Staff && session.HelpOnAtUtc is not null;
    }

    /// <summary>
    /// Whether the actor may turn help on or off for the session: its own
    /// member of staff, while the session is <c>Active</c>. One predicate,
    /// so the Case page and the store's conditional write agree.
    /// </summary>
    public static bool MayTurnHelp(ActionActor actor, GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(session);
        return session.OwnerKind == ActorKind.Staff
            && session.State == GlassRepairEstimateSessionState.Active
            && GlassRepairEstimateSessionPolicy.IsOwner(actor, session);
    }

    /// <summary>
    /// Why the Automation may not act in a member of staff's session now:
    /// <see cref="NotActiveCode"/> once it has left <c>Active</c>,
    /// <see cref="OffCode"/> while help is off; null when it may.
    /// </summary>
    public static string? RefusalOf(GlassRepairEstimateSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.State != GlassRepairEstimateSessionState.Active)
        {
            return NotActiveCode;
        }

        return HelpOn(session) ? null : OffCode;
    }

    /// <summary>
    /// The session the Automation's Glass's tools act in on a Case (design
    /// 08 section 5). Named by <paramref name="sessionId"/>: the Automation's
    /// own, or a member of staff's with <see cref="RefusalOf"/>'s code;
    /// <see cref="NoSessionCode"/> for any other id. Not named: the one
    /// session open to it, which is the Automation's own live session or a
    /// member of staff's <c>Active</c> session with help on;
    /// <see cref="SeveralCode"/> when there is more than one. With none, a
    /// member of staff's <c>Active</c> session with help off is
    /// <see cref="OffCode"/>; otherwise the newer of the Automation's own
    /// ended session (shown as it stands) and a member of staff's helped
    /// session that has since ended (<see cref="NotActiveCode"/>); otherwise
    /// <see cref="NoSessionCode"/>.
    /// </summary>
    public static GlassSessionHelpTarget Find(GlassSessionHelpCandidates candidates, Guid? sessionId)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var automation = candidates.Automation;
        if (sessionId is { } named)
        {
            if (automation?.Id == named)
            {
                return new(automation, null);
            }

            var staff = candidates.Staff.FirstOrDefault(item => item.Id == named);
            return staff is null ? new(null, NoSessionCode) : new(staff, RefusalOf(staff));
        }

        var open = Open(candidates);
        if (open.Count > 1)
        {
            return new(null, SeveralCode);
        }
        if (open.Count == 1)
        {
            return new(open[0], null);
        }
        if (candidates.Staff.Any(item => item.State == GlassRepairEstimateSessionState.Active))
        {
            return new(null, OffCode);
        }

        var ended = candidates.Staff
            .Where(HelpOn)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefault();
        if (ended is not null && (automation is null || ended.CreatedAtUtc > automation.CreatedAtUtc))
        {
            return new(ended, NotActiveCode);
        }

        return automation is null ? new(null, NoSessionCode) : new(automation, null);
    }

    /// <summary>
    /// The sessions a <see cref="SeveralCode"/> answer lists for the caller to
    /// name one of: every session open to the Automation on the Case.
    /// </summary>
    public static IReadOnlyList<GlassRepairEstimateSession> Open(GlassSessionHelpCandidates candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var open = candidates.Staff.Where(item => RefusalOf(item) is null).ToList();
        if (candidates.Automation is { } automation
            && GlassRepairEstimateSessionPolicy.OccupiesAccount(automation.State))
        {
            open.Add(automation);
        }

        return open;
    }
}

/// <summary>
/// The writes help makes on a member of staff's Glass's session (R1170). A
/// port of its own, not <see cref="IGlassRepairEstimateSessionStore"/>, so
/// what acts in a member of staff's session holds no path to save, close or
/// supersede it. Neither write moves the session's version.
/// </summary>
public interface IGlassSessionHelpStore
{
    /// <summary>
    /// Turns help on or off: one conditional write, made only when the actor
    /// owns the session, the session is <c>Active</c> and help is not
    /// already as asked (<see cref="GlassSessionHelpPolicy.MayTurnHelp"/>),
    /// with a history row on the session. The session's version and
    /// concurrency token are left alone, so a toggle never races the return's
    /// claim. False when nothing was written.
    /// </summary>
    Task<bool> SetHelpAsync(Guid sessionId, ActionActor actor, bool on, CancellationToken cancellationToken);

    /// <summary>
    /// The history row of one act the Automation made in a member of staff's
    /// session: its operation key, outcome and what Glass's served; no key,
    /// address or cookie.
    /// </summary>
    Task RecordHelpActAsync(
        Guid sessionId, Guid caseId, ActionActor actor, GlassAutomationAct act, CancellationToken cancellationToken);

    /// <summary>The help act recorded under the operation key on the Case, or null: the same key never sends twice.</summary>
    Task<GlassSessionHelpRecordedAct?> FindHelpActAsync(
        Guid caseId, string operationKey, CancellationToken cancellationToken);
}
