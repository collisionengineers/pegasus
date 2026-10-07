using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Background;
using Pegasus.Web.Pages.Cases;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// The Glass's window while its provider work runs in the background: after a
/// launch or a resume it says Glass's is being prepared, after the provider's
/// return it says the estimate is being brought back.
/// </summary>
/// <remarks>
/// <para>
/// Only the staff member whose session it is sees it; anyone else is told it
/// does not exist. <c>glass-opening.js</c> asks <see cref="OnGetStateAsync"/>
/// whether work is still running and whether the estimator is open. Neither
/// answer carries the estimator address: it carries the session's one-use
/// callback token, so it is only ever the <c>Location</c> of
/// <see cref="OnGetGoAsync"/>.
/// </para>
/// <para>
/// A session left in a state only running work holds, with nothing running
/// for it in this host, was interrupted — a restart, or work past its time —
/// and <see cref="OnGetGoAsync"/> settles it before it answers.
/// </para>
/// </remarks>
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class OpeningModel(
    IGlassRepairEstimateSessionReader glassSessions,
    GlassRepairEstimateAvailability glassAvailability,
    ProviderWorkQueue glassWork,
    TimeProvider timeProvider) : StaffPageModel
{
    public GlassRepairEstimateSession Session { get; private set; } = null!;

    /// <summary>
    /// The words the window shows while it waits: once a return has been
    /// accepted it is bringing the estimate back, before that it is preparing.
    /// </summary>
    public string Waiting => Session.CallbackConsumedAtUtc is null
        ? GlassLabels.Preparing
        : GlassLabels.BringingBack;

    public async Task<IActionResult> OnGetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await OwnSessionAsync(sessionId, cancellationToken) is not { } session)
        {
            return NotFound();
        }

        Session = session;
        return Page();
    }

    public async Task<IActionResult> OnGetStateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await OwnSessionAsync(sessionId, cancellationToken) is not { } session)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            pending = glassWork.IsInFlight(session.Id),
            open = session.State == GlassRepairEstimateSessionState.Active
                && session.ExpiresAtUtc > timeProvider.GetUtcNow(),
        });
    }

    /// <summary>
    /// Where the window goes once nothing runs for the session: the estimator
    /// while the session is open, and back to the Case with what the session
    /// came to when it is not.
    /// </summary>
    public async Task<IActionResult> OnGetGoAsync(
        Guid sessionId,
        [FromServices] IGlassRepairEstimateGateway glassEstimates,
        CancellationToken cancellationToken)
    {
        if (await OwnSessionAsync(sessionId, cancellationToken) is not { } session
            || !TryGetActor(out var actor))
        {
            return NotFound();
        }
        if (glassWork.IsInFlight(session.Id))
        {
            return RedirectToPage("/Integrations/Glass/Opening", new { sessionId });
        }
        if (GlassRepairEstimateSessionPolicy.AwaitsProviderWork(session.State))
        {
            session = await glassEstimates.SettleInterruptedAsync(actor, session.Id, cancellationToken);
        }
        if (await glassEstimates.GetEstimatorUrlAsync(actor, session.Id, cancellationToken) is { } estimator)
        {
            // The Case keeps its edit authority: the operator is inside the
            // provider now and the result lands back on it.
            return Redirect(estimator.AbsoluteUri);
        }

        return DetailsModel.ReportGlassSession(this, session, GlassLabels.LaunchRefused);
    }

    /// <summary>
    /// The staff member's own session, on a host that reaches Glass's. A host
    /// that does not has no window to show: every request here is a 404.
    /// </summary>
    private async Task<GlassRepairEstimateSession?> OwnSessionAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        glassAvailability.Enabled
        && TryGetActor(out var actor)
        && actor.Kind == ActorKind.Staff
        && Guid.TryParse(actor.SubjectId, out var staffId)
            ? await glassSessions.GetOwnAsync(sessionId, staffId, cancellationToken)
            : null;
}
