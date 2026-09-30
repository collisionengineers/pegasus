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
/// The work carries only the proved actor and the session; everything else,
/// the accepted return's own message included, is read back from the session.
/// A refusal the gateway expects — the credential replaced or disabled, the
/// session moved on by someone else — leaves the session as it stands and is
/// logged as a warning with its reason; the Glass's window then reports it.
/// </para>
/// <para>
/// Work admitted to the in-memory queue but lost to a host recycle before it
/// is picked up leaves the session as its prepare half wrote it: Prepared or
/// Importing, which <see cref="IGlassRepairEstimateGateway.SettleInterruptedAsync"/>
/// settles exactly as it settles work stopped mid-run, or, for a reopen, a
/// session Resume takes again as it stands.
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
    IGetCaseHeader caseHeaders,
    IAcquireCaseEditLease leases,
    ILogger<GlassSessionWork> logger)
{
    /// <summary>The provider work a step owes, keyed by its session.</summary>
    public static ProviderWork For(ActionActor actor, GlassRepairEstimateStep step)
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
                    .ImportAsync(actor, sessionId, cancellationToken)),
            _ => throw new ArgumentException("The step owes no provider work.", nameof(step)),
        };
    }

    public async Task LaunchAsync(ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await glassEstimates.ContinueLaunchAsync(new(actor, sessionId), cancellationToken);
        }
        catch (Exception refusal) when (IsExpectedRefusal(refusal))
        {
            LogWorkRefused(logger, "GlassLaunch", sessionId, Reason(refusal));
        }
    }

    public async Task ImportAsync(ActionActor actor, Guid sessionId, CancellationToken cancellationToken)
    {
        GlassRepairEstimateSession session;
        try
        {
            session = await glassEstimates.ContinueImportAsync(new(actor, sessionId), cancellationToken);
        }
        catch (Exception refusal) when (IsExpectedRefusal(refusal))
        {
            LogWorkRefused(logger, "GlassImport", sessionId, Reason(refusal));
            return;
        }
        if (session.State == GlassRepairEstimateSessionState.AwaitingImport)
        {
            await LandHeldEstimateAsync(actor, session, cancellationToken);
        }
    }

    private static bool IsExpectedRefusal(Exception exception) =>
        exception is GlassRepairEstimateRefusalException
            or GlassRepairEstimateSessionConflictException
            or StaffAuthorizationException;

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
            var current = await caseHeaders.ExecuteAsync(new(session.CaseId, actor), cancellationToken);
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

    [LoggerMessage(
        EventId = 1214,
        Level = LogLevel.Warning,
        Message = "Glass's {Kind} for session {SessionId} was refused and left the session as it stands: {Reason}")]
    private static partial void LogWorkRefused(ILogger logger, string kind, Guid sessionId, string reason);
}
