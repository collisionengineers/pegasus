using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Pages.Cases;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// Where the operator's own browser lands after Save &amp; Exit in Glass's
/// (CASE-047 B04), carrying the provider's message on the address it was
/// launched with.
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
/// </remarks>
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CallbackModel(
    IGlassRepairEstimateGateway glassEstimates,
    IGlassRepairEstimateSessionReader glassSessions) : StaffPageModel
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
            // Another Engineer's session, refused before its one-use token is
            // spent: the owner can still return on the same address.
            return Forbid();
        }

        try
        {
            var completed = await CompleteAsync(actor, session, correlation, cancellationToken);
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

    /// <summary>The roles the page used to declare; every staff role may return.</summary>
    private static bool IsStaff(ActionActor actor) =>
        actor.IsInRole(StaffRole.Administrator)
        || actor.IsInRole(StaffRole.Engineer)
        || actor.IsInRole(StaffRole.User);

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
    /// The operator's browser arrives here in the window Glass's ran in, so
    /// the Estimate section is handed back to the Case window rather than
    /// rendered here (<see cref="DetailsModel.GlassReturn(PageModel, Guid)"/>).
    /// </summary>
    private PartialViewResult Estimate(Guid caseId) => DetailsModel.GlassReturn(this, caseId);
}
