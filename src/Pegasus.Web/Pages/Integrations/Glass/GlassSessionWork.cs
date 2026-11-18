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
/// Importing, which <see cref="IGlassRepairEstimateGateway.SettleInterruptedAsync(ActionActor, Guid, CancellationToken)"/>
/// settles exactly as it settles work stopped mid-run, or, for a reopen, a
/// session Resume takes again as it stands.
/// </para>
/// <para>
/// <b>A held estimate lands on the return.</b> When the launch's edit
/// authority is no longer current — the Case was saved, handed on or taken back
/// into edit mode while Glass's was open — the import lands the estimate as the
/// Current repair spec under the returning staff member's own session: their
/// live lease where they hold one, else a fresh lease taken for them (operator,
/// 6 October 2026). The import advances the Case without ending that session,
/// so a page they keep open catches up on it as it does on any system write.
/// Only a Case another staff member holds waits for Resume, so nobody's edit
/// session is overtaken (FRD-25).
/// </para>
/// <para>
/// <b>A build's estimate is reopened in the same window.</b> Once a build's
/// return has landed as a new Glass's repair spec, the same work reopens that
/// spec's estimate for the member of staff who ran it, exactly as Glass's on
/// that spec reopens it, so the Glass's window opens it with no second press
/// (operator, 18 November 2026; R1171). The reopen's session is held busy for
/// the whole of it, so the Case page waits on it rather than settling it. A
/// reopen that cannot go ahead leaves the new spec on the Case, where Glass's
/// reopens it; its operation key is the build's own, so it is never sent twice.
/// </para>
/// </remarks>
public sealed partial class GlassSessionWork(
    IGlassRepairEstimateGateway glassEstimates,
    IGetCaseHeader caseHeaders,
    CaseEditLeases leases,
    ProviderWorkQueue glassWork,
    ILogger<GlassSessionWork> logger)
{
    /// <summary>
    /// A build's own time cap: the launch, every act at a browser's pace, the
    /// save, the export and the import run as one piece of work, which may
    /// pass <see cref="ProviderWorkService.TimeCap"/>. A build is never split:
    /// taking up an editor session again after a restart could send a post
    /// twice.
    /// </summary>
    public static readonly TimeSpan BuildTimeCap = TimeSpan.FromMinutes(15);

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
            GlassRepairEstimateContinuation.Build => new(
                sessionId,
                "GlassBuild",
                (services, cancellationToken) => services.GetRequiredService<GlassSessionWork>()
                    .BuildAsync(actor, sessionId, cancellationToken),
                Cap: BuildTimeCap),
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

    public Task ImportAsync(ActionActor actor, Guid sessionId, CancellationToken cancellationToken) =>
        LandAsync("GlassImport", actor, sessionId, glassEstimates.ContinueImportAsync, cancellationToken);

    /// <summary>
    /// A build, through its own return and import; a held estimate then lands
    /// as an import's does, and the new spec's estimate is then reopened for
    /// the member of staff in the same window (R1171).
    /// </summary>
    public Task BuildAsync(ActionActor actor, Guid sessionId, CancellationToken cancellationToken) =>
        LandAsync("GlassBuild", actor, sessionId, glassEstimates.ContinueBuildAsync, cancellationToken);

    private async Task LandAsync(
        string kind,
        ActionActor actor,
        Guid sessionId,
        Func<GlassRepairEstimateContinueRequest, CancellationToken, Task<GlassRepairEstimateSession>> work,
        CancellationToken cancellationToken)
    {
        GlassRepairEstimateSession session;
        try
        {
            session = await work(new(actor, sessionId), cancellationToken);
        }
        catch (Exception refusal) when (IsExpectedRefusal(refusal))
        {
            LogWorkRefused(logger, kind, sessionId, Reason(refusal));
            return;
        }
        if (session.State == GlassRepairEstimateSessionState.AwaitingImport)
        {
            session = await LandHeldEstimateAsync(actor, session, cancellationToken);
        }
        if (session is { State: GlassRepairEstimateSessionState.Completed, Build.BuiltSpecificationId: { } built })
        {
            await ReopenBuiltAsync(actor, session, built, cancellationToken);
        }
    }

    /// <summary>
    /// The estimate a build landed as a new Glass's repair spec, reopened for
    /// the member of staff who ran it with the launch Glass's on that spec
    /// makes: the reopen of a linked spec, under their own edit session, taken
    /// as a held estimate's landing takes it (operator, 18 November 2026;
    /// R1171).
    /// </summary>
    /// <remarks>
    /// The new session's id is held busy from before its claim until its
    /// provider work has run here, so the Case page's Glass's waits on it
    /// rather than settling it, and the build's window, still waiting on the
    /// build, then follows on to it. The operation key is the build's own with
    /// a suffix: a second run names the launch already made and sends nothing.
    /// The build already succeeded, so a reopen that cannot go ahead is logged
    /// and the new spec stays on the Case, where Glass's reopens it.
    /// </remarks>
    private async Task ReopenBuiltAsync(
        ActionActor actor, GlassRepairEstimateSession built, Guid specificationId,
        CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        using (glassWork.Reserve(sessionId))
        {
            try
            {
                var (lease, refusal) = await TakeLeaseAsync(actor, built.CaseId, cancellationToken);
                if (lease is null)
                {
                    LogBuiltNotReopened(logger, built.CaseId, built.Id, refusal);
                    return;
                }

                var step = await glassEstimates.PrepareLaunchAsync(
                    new GlassRepairEstimateLaunchRequest(
                        actor,
                        built.CaseId,
                        lease.Version,
                        lease.Token,
                        GlassRepairEstimateSessionPolicy.BuildReopenOperationKey(built),
                        sessionId)
                    {
                        SpecificationId = specificationId,
                    },
                    cancellationToken);
                if (step.Continuation == GlassRepairEstimateContinuation.Launch)
                {
                    await glassEstimates.ContinueLaunchAsync(new(actor, step.Session.Id), cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogBuiltReopenFailed(logger, built.CaseId, built.Id, Reason(exception), exception);
            }
        }
    }

    private static bool IsExpectedRefusal(Exception exception) =>
        exception is GlassRepairEstimateRefusalException
            or GlassRepairEstimateSessionConflictException
            or StaffAuthorizationException;

    /// <summary>
    /// Lands a held estimate under the returning staff member's session: the
    /// live lease they hold, resumed as the Case page resumes it, or a fresh
    /// lease when nobody holds the Case. A Case another staff member holds, or
    /// one that is no longer writable, keeps the estimate held for Resume and
    /// says so. The return itself already succeeded, so a landing that fails
    /// anywhere — reading the Case, taking the lease or the import itself — is
    /// logged and leaves the session as it stands. A lease taken for a Resume
    /// that does not import stays the staff member's own edit session, which
    /// the Case page picks back up. Answers the session as the landing left it.
    /// </summary>
    private async Task<GlassRepairEstimateSession> LandHeldEstimateAsync(
        ActionActor actor, GlassRepairEstimateSession session, CancellationToken cancellationToken)
    {
        try
        {
            var (lease, refusal) = await TakeLeaseAsync(actor, session.CaseId, cancellationToken);
            if (lease is null)
            {
                LogHeldEstimateWaits(logger, session.CaseId, session.Id, refusal);
                return session;
            }

            var step = await glassEstimates.PrepareResumeAsync(
                new(actor, session.Id, session.Version, lease.Version, lease.Token),
                cancellationToken);
            return step.Continuation == GlassRepairEstimateContinuation.Import
                ? await glassEstimates.ContinueImportAsync(new(actor, session.Id), cancellationToken)
                : step.Session;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogHeldEstimateNotLanded(logger, session.CaseId, session.Id, Reason(exception), exception);
            return session;
        }
    }

    /// <summary>
    /// The staff member's own edit session on the Case, for work that lands
    /// on it after their request has answered: the live lease they hold,
    /// resumed as the Case page resumes it, or a fresh lease when nobody holds
    /// the Case. A Case that is no longer writable, or that another staff
    /// member holds, answers no lease and the reason.
    /// </summary>
    private async Task<(CaseEditLease? Lease, string Refusal)> TakeLeaseAsync(
        ActionActor actor, Guid caseId, CancellationToken cancellationToken)
    {
        var current = await caseHeaders.ExecuteAsync(new(caseId, actor), cancellationToken);
        if (current is null
            || current.Workflow.Archive is not null
            || !AssessmentPolicy.IsWritableState(current.Workflow.State))
        {
            return (null, "the Case is not writable");
        }

        if (current.ActiveEditLease is { } held)
        {
            if (!CaseEditAuthority.IsHolder(held.HolderKind, held.Holder, actor))
            {
                return (null, "another staff member holds the Case");
            }
            var resumed = await leases.ResumeAsync(new(caseId, actor), cancellationToken);
            if (resumed is null)
            {
                return (null, "the staff member's lease could not be resumed");
            }
            return (resumed, string.Empty);
        }

        var claimed = await leases.ClaimAsync(
            new(caseId, current.Workflow.Version, actor, StaffPageModel.NewOperationKey()),
            cancellationToken);
        return (claimed, string.Empty);
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

    [LoggerMessage(
        EventId = 1215,
        Level = LogLevel.Warning,
        Message = "Glass's held estimate on case {CaseId} (session {SessionId}) waits for Resume: {Reason}")]
    private static partial void LogHeldEstimateWaits(ILogger logger, Guid caseId, Guid sessionId, string reason);

    [LoggerMessage(
        EventId = 1217,
        Level = LogLevel.Warning,
        Message = "Glass's built estimate on case {CaseId} (build session {SessionId}) was not reopened and stays on the Case for Glass's to reopen: {Reason}")]
    private static partial void LogBuiltNotReopened(ILogger logger, Guid caseId, Guid sessionId, string reason);

    [LoggerMessage(
        EventId = 1218,
        Level = LogLevel.Warning,
        Message = "Glass's built estimate on case {CaseId} (build session {SessionId}) was not reopened and stays on the Case for Glass's to reopen: {Reason}")]
    private static partial void LogBuiltReopenFailed(
        ILogger logger, Guid caseId, Guid sessionId, string reason, Exception exception);
}
