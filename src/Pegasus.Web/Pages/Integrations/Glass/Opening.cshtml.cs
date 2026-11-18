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
/// launch or a resume it says Glass's is being prepared, while a build runs it
/// says how far the build has gone, and after the provider's return it says
/// the estimate is being brought back.
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
/// <para>
/// A build's own work reopens the estimate it landed (R1171), so once the
/// build is done <see cref="OnGetGoAsync"/> sends this window on to that
/// session's own window, which opens the estimator here as after any launch.
/// </para>
/// </remarks>
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class OpeningModel(
    IGlassRepairEstimateSessionReader glassSessions,
    GlassRepairEstimateAvailability glassAvailability,
    ProviderWorkQueue glassWork) : StaffPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    public GlassRepairEstimateSession Session { get; private set; } = null!;

    /// <summary>
    /// The words the window shows while it waits: once a return has been
    /// accepted it is bringing the estimate back; before that, a build says it
    /// is building and anything else that it is preparing.
    /// </summary>
    public string Waiting => Session.CallbackConsumedAtUtc is not null
        ? GlassLabels.BringingBack
        : Building is not null ? GlassLabels.Building : GlassLabels.Preparing;

    /// <summary>
    /// How far the build has gone, "12 of 34", while one runs and no return
    /// has been accepted (operator, 18 November 2026; R1171); otherwise null.
    /// </summary>
    public string? Building => BuildRuns(Session.State) ? ProgressOf(Session.Build) : null;

    public async Task<IActionResult> OnGetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await OwnSessionAsync(sessionId, cancellationToken) is not { } session)
        {
            return NotFound();
        }

        Session = session;
        return Page();
    }

    /// <summary>
    /// Whether work still runs, whether the estimator is open, and, while a
    /// build runs, how far it has gone.
    /// </summary>
    public async Task<IActionResult> OnGetStateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!TryGetOwnerId(out var staffId)
            || await glassSessions.GetOwnProgressAsync(sessionId, staffId, cancellationToken) is not { } progress)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            pending = glassWork.IsInFlight(sessionId),
            open = progress.State == GlassRepairEstimateSessionState.Active,
            building = BuildRuns(progress.State) ? ProgressOf(progress.Build) : null,
        });
    }

    /// <summary>The states a build runs in, before its own return is claimed.</summary>
    private static bool BuildRuns(GlassRepairEstimateSessionState state) => state is
        GlassRepairEstimateSessionState.Prepared or GlassRepairEstimateSessionState.Launching
        or GlassRepairEstimateSessionState.Active;

    private static string? ProgressOf(GlassEstimateBuild? build) =>
        build is { Running: true } running ? GlassLabels.BuildProgress(running.Done, running.Total) : null;

    /// <summary>
    /// Where the window goes once nothing runs for the session: the estimator
    /// while the session is open, the session that reopened a completed
    /// build's estimate, and back to the Case with what the session came to
    /// otherwise.
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
        if (await ReopenedBuildAsync(session, staffId, cancellationToken) is { } reopened)
        {
            // The build's own work reopened the estimate it landed (R1171):
            // this window waits on that session, which hands off and opens
            // the estimator here.
            return RedirectToPage("/Integrations/Glass/Opening", new { sessionId = reopened });
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

        return DetailsModel.ReportGlassSession(this, session, GlassLabels.LaunchRefused);
    }

    /// <summary>
    /// The session that reopened a completed build's estimate for its member
    /// of staff (operator, 18 November 2026; R1171): their newest session on
    /// the Case, when it is another one on the build's vehicle that is open at
    /// Glass's or still has work running. Null for any other session, and when
    /// the reopen did not open, so the window reports the build as after any
    /// return. Only a completed build costs the read.
    /// </summary>
    private async Task<Guid?> ReopenedBuildAsync(
        GlassRepairEstimateSession session, Guid staffId, CancellationToken cancellationToken)
    {
        if (session is not
            {
                State: GlassRepairEstimateSessionState.Completed,
                Build.BuiltSpecificationId: not null,
                ProviderVehicleId: { } vehicle
            })
        {
            return null;
        }

        var newest = await glassSessions.GetForCaseAsync(session.CaseId, staffId, cancellationToken);
        if (newest is null
            || newest.Id == session.Id
            || !string.Equals(newest.ProviderVehicleId, vehicle, StringComparison.Ordinal)
            || (newest.State != GlassRepairEstimateSessionState.Active && !glassWork.IsInFlight(newest.Id)))
        {
            return null;
        }

        return newest.Id;
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
