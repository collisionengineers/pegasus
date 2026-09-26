using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Pages.Cases;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// Where the operator's own browser lands after Save &amp; Exit in Glass's,
/// carrying the provider's message on the address it was launched with.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both verbs, one act.</b> Glass's returns the operator by navigating to the
/// <c>caller</c> the launch handed it, and its own callback is relayed with
/// <see cref="HttpMethod.Get"/> (<c>GlassMvaClient.RelayCallbackAsync</c>); the
/// provider may equally post that address. Either way the message is the query
/// it arrived on, so both verbs read the same thing and do the same thing.
/// </para>
/// <para>
/// <b>No staff token can be asked for.</b> The request is composed by the
/// provider, so it carries no antiforgery token; it is refused instead on what
/// it does carry — the one-use token in its own path, and the signed-in staff
/// member who owns the session that token names. The staff cookie is
/// SameSite=Strict, and the provider's return is a cross-site navigation, so
/// the browser withholds the cookie on that first arrival: a return that
/// arrives cross-site without a session is answered with
/// <c>Shared/_GlassBounce</c>, which asks for the same address again from
/// Pegasus's own origin, where the cookie travels. A return that still has no
/// session after that is sent to sign in and back to the same address, whole. <b>Nothing is read out of the
/// query to decide anything</b>: no identity, no role, no case. The query is
/// handed to the gateway exactly as it arrived, because it is the provider's
/// message and re-encoding it would change what Glass's verifies.
/// </para>
/// <para>
/// <b>The token is spent only by the owner.</b> The callback's identity is the
/// persisted one-use correlation and the fingerprint of the query delivered
/// under it — no operation key stands in for either. Who may act on it is
/// derived here, on the server, from the current signed-in staff member: an
/// unknown token is a 404 and a signed-in stranger is a 403, both before
/// anything is written, so neither can consume a session they did not launch.
/// The Engineer who did gets the same answer however many times the browser
/// repeats the return, because the gateway reads back what the first delivery
/// produced rather than acting on it twice.
/// </para>
/// <para>
/// <b>A held estimate lands when the Case is free.</b> When the launch's edit
/// authority is no longer current — the Case was saved while Glass's was open
/// — the return takes a fresh lease for the returning staff member and lands
/// the estimate as the Current repair spec, provided nobody holds the Case.
/// While anyone holds it, the same staff member in another window included,
/// the estimate waits for Resume, so unsaved edits are never overtaken
/// (FRD-25).
/// </para>
/// </remarks>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class CallbackModel(
    IGlassRepairEstimateGateway glassEstimates,
    IGlassRepairEstimateSessionReader glassSessions,
    IGetCase cases,
    IAcquireCaseEditLease leases,
    ILogger<CallbackModel> logger) : StaffPageModel
{
    public Task<IActionResult> OnGetAsync(string correlation, CancellationToken cancellationToken) =>
        DeliverAsync(correlation, cancellationToken);

    public Task<IActionResult> OnPostAsync(string correlation, CancellationToken cancellationToken) =>
        DeliverAsync(correlation, cancellationToken);

    private async Task<IActionResult> DeliverAsync(
        string correlation,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            // The browser names where a navigation came from; a cross-site
            // arrival is the provider's own and is bounced once through this
            // origin. Anything else without a session is a signed-out operator.
            return string.Equals(Request.Headers["Sec-Fetch-Site"], "cross-site", StringComparison.OrdinalIgnoreCase)
                ? Partial("_GlassBounce", Request.Path.Value + Request.QueryString.Value)
                : Challenge();
        }
        if (!TryGetActor(out var actor) || !IsStaff(actor))
        {
            return Forbid();
        }

        var session = await glassSessions.FindByCallbackAsync(correlation, cancellationToken);
        if (session is null)
        {
            return NotFound();
        }
        if (!Guid.TryParse(actor.SubjectId, out var staffId) || staffId != session.PegasusUserId)
        {
            // Another staff member's session, refused before its one-use token is
            // spent: the owner can still return on the same address.
            return Forbid();
        }

        try
        {
            var completed = await CompleteAsync(actor, session, correlation, cancellationToken);
            if (completed.State == GlassRepairEstimateSessionState.AwaitingImport)
            {
                completed = await LandHeldEstimateAsync(actor, completed, cancellationToken);
            }
            return DetailsModel.ReportSessionOutcome(
                completed, TempData, () => Estimate(completed.CaseId));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (GlassRepairEstimateSessionConflictException)
        {
            // A session that is not waiting for this message, or a second and
            // different message for one that already answered. Nothing was
            // recorded, and the Case says so where the operator is going. Every
            // refusal the gateway has for a return is this one type; anything
            // else is a fault and surfaces as one.
            TempData["CaseError"] = GlassLabels.NotImported;
            return Estimate(session.CaseId);
        }
    }

    private static bool IsStaff(ActionActor actor) => actor.Kind == ActorKind.Staff;

    /// <summary>
    /// The provider's message travels verbatim: its identity is the correlation
    /// and the fingerprint the gateway takes of the query, nothing else.
    /// </summary>
    private Task<GlassRepairEstimateSession> CompleteAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        string correlation,
        CancellationToken cancellationToken) =>
        glassEstimates.CompleteAsync(
            new GlassRepairEstimateCallback(
                actor,
                session.Id,
                session.Version,
                correlation,
                Request.QueryString.Value ?? string.Empty),
            cancellationToken);

    /// <summary>
    /// Lands a held estimate on a fresh lease when nobody holds the Case and
    /// it is still writable; otherwise, or when the Case is taken first, the
    /// estimate stays held for Resume. The return itself already succeeded,
    /// so a landing that fails anywhere — reading the Case, taking the lease
    /// or the import itself — is logged and reports the session as it now
    /// stands, never an error page. A lease taken for a Resume that does not
    /// import stays the staff member's own edit session, which the Case page
    /// picks back up.
    /// </summary>
    private async Task<GlassRepairEstimateSession> LandHeldEstimateAsync(
        ActionActor actor,
        GlassRepairEstimateSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            var current = await cases.ExecuteAsync(new(session.CaseId, actor), cancellationToken);
            if (current is null
                || current.ActiveEditLease is not null
                || current.Workflow.Archive is not null
                || !AssessmentPolicy.IsWritableState(current.Workflow.State))
            {
                return session;
            }

            var lease = await leases.ExecuteAsync(
                new(session.CaseId, current.Workflow.Version, actor, NewOperationKey()),
                cancellationToken);
            return await glassEstimates.ResumeAsync(
                new(actor, session.Id, session.Version, current.Workflow.Version, lease.Token),
                cancellationToken);
        }
        catch (Exception exception) when (exception
            is not OperationCanceledException
            and not StaffAuthorizationException)
        {
            LogHeldEstimateNotLanded(logger, session.CaseId, session.Id, Reason(exception), exception);
        }

        return await SessionAsItStandsAsync(session, cancellationToken);
    }

    /// <summary>
    /// The session as the store now has it, or the held one the return
    /// produced when even that read fails: the operator is told where the
    /// session stands either way.
    /// </summary>
    private async Task<GlassRepairEstimateSession> SessionAsItStandsAsync(
        GlassRepairEstimateSession session,
        CancellationToken cancellationToken)
    {
        try
        {
            return await glassSessions.GetForCaseAsync(
                    session.CaseId, session.PegasusUserId, cancellationToken)
                ?? session;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogHeldEstimateNotLanded(logger, session.CaseId, session.Id, Reason(exception), exception);
            return session;
        }
    }

    private static string Reason(Exception exception) =>
        exception is GlassRepairEstimateSessionConflictException conflict
            ? $"{conflict.GetType().Name}:{conflict.Conflict}"
            : exception.GetType().Name;

    [LoggerMessage(
        EventId = 1213,
        Level = LogLevel.Warning,
        Message = "Glass's held estimate on case {CaseId} (session {SessionId}) did not land on the return and stays held for Resume: {Reason}")]
    private static partial void LogHeldEstimateNotLanded(
        ILogger logger, Guid caseId, Guid sessionId, string reason, Exception exception);

    /// <summary>
    /// The operator's browser arrives here in the window Glass's ran in, so
    /// the Estimate section is handed back to the Case window rather than
    /// rendered here (<see cref="DetailsModel.GlassReturn(PageModel, Guid)"/>).
    /// </summary>
    private PartialViewResult Estimate(Guid caseId) => DetailsModel.GlassReturn(this, caseId);
}
