using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
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
    IGetCaseSectionFrame caseFrames) : StaffPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    public GlassRepairEstimateSession Session { get; private set; } = null!;

    /// <summary>The Case the window belongs to, by reference and registration (v52 F, R1234).</summary>
    public string? CaseLine { get; private set; }

    /// <summary>
    /// The words the window shows while it waits: once a return has been
    /// accepted it is bringing the estimate back, before that it is preparing.
    /// </summary>
    public string Waiting => Session.CallbackConsumedAtUtc is null
        ? GlassLabels.Preparing
        : GlassLabels.BringingBack;

    public async Task<IActionResult> OnGetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await OwnSessionAsync(sessionId, cancellationToken) is not { } session
            || !TryGetActor(out var actor))
        {
            return NotFound();
        }

        Session = session;
        CaseLine = await GlassWaitingView.CaseLineAsync(caseFrames, actor, session.CaseId, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnGetStateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var staffId)
            || await glassSessions.GetOwnStateAsync(sessionId, staffId, cancellationToken) is not { } state)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            pending = glassWork.IsInFlight(sessionId),
            open = state == GlassRepairEstimateSessionState.Active,
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
        // The session is read once; the gateway is handed what was read.
        if (!TryGetOwnerId(out var staffId)
            || await glassSessions.GetOwnMaterialAsync(sessionId, staffId, cancellationToken) is not { } read
            || !TryGetActor(out var actor))
        {
            return NotFound();
        }
        var session = read.Session;
        if (glassWork.IsInFlight(session.Id))
        {
            return RedirectToPage("/Integrations/Glass/Opening", new { sessionId });
        }
        Uri? estimator;
        if (GlassRepairEstimateSessionPolicy.AwaitsProviderWork(session.State))
        {
            // A settle writes Prepared or Unknown, neither of which has an
            // estimator to open; only a session something else moved on to
            // Active first is read afresh for its address.
            session = await glassEstimates.SettleInterruptedAsync(actor, read, cancellationToken);
            estimator = session.State == GlassRepairEstimateSessionState.Active
                ? await glassEstimates.GetEstimatorUrlAsync(actor, session.Id, cancellationToken)
                : null;
        }
        else
        {
            estimator = await glassEstimates.GetEstimatorUrlAsync(actor, read, cancellationToken);
        }
        if (estimator is not null)
        {
            // The Case keeps its edit authority: the operator is inside the
            // provider now and the result lands back on it.
            return Redirect(estimator.AbsoluteUri);
        }

        // The window keeps naming its Case as it hands the outcome back.
        return DetailsModel.ReportGlassSession(
            this,
            session,
            GlassLabels.LaunchRefused,
            await GlassWaitingView.CaseLineAsync(caseFrames, actor, session.CaseId, cancellationToken));
    }

    /// <summary>
    /// The staff member's own session, on a host that reaches Glass's. A host
    /// that does not has no window to show: every request here is a 404.
    /// </summary>
    private async Task<GlassRepairEstimateSession?> OwnSessionAsync(
        Guid sessionId, CancellationToken cancellationToken) =>
        TryGetOwnerId(out var staffId)
            ? await glassSessions.GetOwnAsync(sessionId, staffId, cancellationToken)
            : null;

    private bool TryGetOwnerId(out Guid staffId)
    {
        staffId = default;
        return glassAvailability.Enabled
            && TryGetActor(out var actor)
            && actor.Kind == ActorKind.Staff
            && Guid.TryParse(actor.SubjectId, out staffId);
    }
}
