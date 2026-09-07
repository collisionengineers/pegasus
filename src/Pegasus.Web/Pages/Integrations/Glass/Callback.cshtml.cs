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
/// <b>It is a GET, and only a GET.</b> Glass's returns the operator by
/// navigating to the <c>caller</c> the launch handed it, and Pegasus relays that
/// same message back to the provider's own callback with
/// <see cref="HttpMethod.Get"/> — see <c>GlassMvaClient.RelayCallbackAsync</c>.
/// Nothing in the provider's flow posts here, so no post handler is written for
/// one that does not exist.
/// </para>
/// <para>
/// <b>No staff token can be asked for.</b> The request is composed by the
/// provider, so it carries no antiforgery token; it is refused instead on what
/// it does carry — the one-use token in its own path, and the signed-in staff
/// member who owns the session that token names. Nothing is read from the query
/// to decide either: it is handed to the gateway exactly as it arrived, because
/// it is the provider's message and re-encoding it would change what Glass's
/// verifies.
/// </para>
/// <para>
/// <b>The token is spent only by the owner.</b> An unknown token is a 404 and a
/// signed-in stranger is a 403, both before anything is written, so neither can
/// consume a session they did not launch. The Engineer who did gets the same
/// answer however many times the browser repeats the return: the gateway reads
/// back what the first delivery produced rather than acting on it twice.
/// </para>
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[IgnoreAntiforgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CallbackModel(
    GlassRepairEstimateGateway glassEstimates,
    IGlassRepairEstimateSessionReader glassSessions) : StaffPageModel
{
    public async Task<IActionResult> OnGetAsync(
        string correlation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
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
            var completed = await glassEstimates.CompleteAsync(
                new GlassRepairEstimateCallbackDelivery(
                    new GlassRepairEstimateCallback(
                        actor,
                        session.Id,
                        session.Version,
                        correlation,
                        $"{session.OperationKey}:callback"),
                    Request.QueryString.Value ?? string.Empty),
                cancellationToken);
            return DetailsModel.ReportSessionOutcome(
                completed, TempData, () => Estimate(completed.CaseId));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception refusal)
            when (refusal is GlassRepairEstimateSessionConflictException
                or ArgumentException
                or InvalidOperationException)
        {
            // A session that is not waiting for this message, or a second and
            // different message for one that already answered. Nothing was
            // recorded, and the Case says so where the operator is going.
            TempData["CaseError"] = GlassLabels.NotImported;
            return Estimate(session.CaseId);
        }
    }

    private RedirectToPageResult Estimate(Guid caseId) =>
        RedirectToPage("/Cases/Details", new { id = caseId, section = "estimate" });
}
