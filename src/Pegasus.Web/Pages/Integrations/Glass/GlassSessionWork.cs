using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Background;

namespace Pegasus.Web.Pages.Integrations.Glass;

/// <summary>
/// The Glass's provider work a launch, a resume or an accepted return leaves
/// owed, run by <see cref="ProviderWorkService"/> for the staff member who
/// asked for it after their request has answered.
/// </summary>
/// <remarks>
/// <para>
/// The work carries the proved actor and, for a return, the provider's own
/// message; it reads everything else back from the session. The message lives
/// only in memory: work lost with the process is settled as an interrupted
/// request's would be, and the export is later looked up, never relayed again.
/// </para>
/// <para>
/// <b>A held estimate lands when the Case is free.</b> When the launch's edit
/// authority is no longer current — the Case was saved while Glass's was open —
/// the import takes a fresh lease for the returning staff member and lands the
/// estimate as the Current repair spec, provided nobody holds the Case. While
/// anyone holds it, the same staff member in another window included, the
/// estimate waits for Resume, so unsaved edits are never overtaken (FRD-25).
/// </para>
/// </remarks>
public sealed partial class GlassSessionWork(
    IGlassRepairEstimateGateway glassEstimates,
    IGetCase cases,
    IAcquireCaseEditLease leases,
    ILogger<GlassSessionWork> logger)
{
    /// <summary>The provider work a step owes, keyed by its session.</summary>
    public static ProviderWork For(ActionActor actor, GlassRepairEstimateStep step, string? rawQuery = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(step);
        var sessionId = step.Session.Id;
        return step.Continuation switch
        {
            GlassRepairEstimateContinuation.Launch => new(
                sessionId,
                "GlassLaunch",
                (services, cancellationToken) => services.GetRequiredService<GlassSessionWork>()
                    .LaunchAsync(actor, sessionId, cancellationToken)),
            GlassRepairEstimateContinuation.Import => new(
                sessionId,
                "GlassImport",
                (services, cancellationToken) => services.GetRequiredService<GlassSessionWork>()
                    .ImportAsync(actor, sessionId, rawQuery, cancellationToken)),
            _ => throw new ArgumentException("The step owes no provider work.", nameof(step)),
        };
    }

    public Task LaunchAsync(ActionActor actor, Guid sessionId, CancellationToken cancellationToken) =>
        glassEstimates.ContinueLaunchAsync(new(actor, sessionId), cancellationToken);

    public async Task ImportAsync(
        ActionActor actor, Guid sessionId, string? rawQuery, CancellationToken cancellationToken)
    {
        var session = await glassEstimates.ContinueImportAsync(new(actor, sessionId, rawQuery), cancellationToken);
        if (session.State == GlassRepairEstimateSessionState.AwaitingImport)
        {
            await LandHeldEstimateAsync(actor, session, cancellationToken);
        }
    }

    /// <summary>
    /// Lands a held estimate on a fresh lease when nobody holds the Case and it
    /// is still writable; otherwise, or when the Case is taken first, the
    /// estimate stays held for Resume. The return itself already succeeded, so
    /// a landing that fails anywhere — reading the Case, taking the lease or
    /// the import itself — is logged and leaves the session as it stands. A
    /// lease taken for a Resume that does not import stays the staff member's
    /// own edit session, which the Case page picks back up.
    /// </summary>
    private async Task LandHeldEstimateAsync(
        ActionActor actor, GlassRepairEstimateSession session, CancellationToken cancellationToken)
    {
        try
        {
            var current = await cases.ExecuteAsync(new(session.CaseId, actor), cancellationToken);
            if (current is null
                || current.ActiveEditLease is not null
                || current.Workflow.Archive is not null
                || !AssessmentPolicy.IsWritableState(current.Workflow.State))
            {
                return;
            }

            var lease = await leases.ExecuteAsync(
                new(session.CaseId, current.Workflow.Version, actor, StaffPageModel.NewOperationKey()),
                cancellationToken);
            var step = await glassEstimates.PrepareResumeAsync(
                new(actor, session.Id, session.Version, current.Workflow.Version, lease.Token),
                cancellationToken);
            if (step.Continuation == GlassRepairEstimateContinuation.Import)
            {
                await glassEstimates.ContinueImportAsync(new(actor, session.Id), cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogHeldEstimateNotLanded(logger, session.CaseId, session.Id, Reason(exception), exception);
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
}
