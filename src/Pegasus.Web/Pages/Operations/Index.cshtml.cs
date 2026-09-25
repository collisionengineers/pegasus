using System.Collections.Immutable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Notifications;
using Pegasus.Core.Operations;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Operations;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ValidateAntiForgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    GetRequestOperations getRequestOperations,
    RetryExternalWork retryExternalWork,
    IAiJobQueries aiJobQueries,
    ICreateAiJob createAiJob,
    IConfirmAiJob confirmAiJob,
    ICancelAiJob cancelAiJob,
    IUnidentifiedStore unidentifiedStore,
    IEvaSubmissionQueries evaSubmissionQueries,
    IStaffAccountQueries staffAccounts,
    TimeProvider timeProvider,
    IListIntakeLog listIntakeLog) : StaffPageModel
{
    /// <summary>
    /// Failed intake (Received file D2, 13 September), one row per failure kind:
    /// failed allocation (Retry allocation), failed OCR (Retry OCR) and other
    /// processing failures (Re-evaluate). The Intake log is Administrators only,
    /// so the rows are read (and rendered) only for an Administrator; the actions
    /// post to the one owner, Administration › Logs.
    /// </summary>
    public IReadOnlyList<IntakeLogActionableFailure> FailedIntake { get; private set; } = [];

    /// <summary>The failure kinds Operations lists, in the order it lists them.</summary>
    public static readonly IReadOnlyList<IntakeLogOutcome> FailureKinds = IntakeLogPolicy.RetryableFailures;

    /// <summary>
    /// What one Unidentified-resolution job is asked to do. FRD-27 gives this
    /// kind "the U reference only" as its input, so the direction is fixed
    /// rather than typed: it is the pointer's payload, never operator copy.
    /// </summary>
    private const string UnidentifiedInstruction =
        "Propose a destination for this Unidentified item and give the reason.";

    private readonly GetRequestOperations getRequestOperations =
        getRequestOperations ?? throw new ArgumentNullException(nameof(getRequestOperations));
    private readonly RetryExternalWork retryExternalWork =
        retryExternalWork ?? throw new ArgumentNullException(nameof(retryExternalWork));
    private readonly IAiJobQueries aiJobQueries =
        aiJobQueries ?? throw new ArgumentNullException(nameof(aiJobQueries));
    private readonly ICreateAiJob createAiJob =
        createAiJob ?? throw new ArgumentNullException(nameof(createAiJob));
    private readonly IConfirmAiJob confirmAiJob =
        confirmAiJob ?? throw new ArgumentNullException(nameof(confirmAiJob));
    private readonly ICancelAiJob cancelAiJob =
        cancelAiJob ?? throw new ArgumentNullException(nameof(cancelAiJob));
    private readonly IUnidentifiedStore unidentifiedStore =
        unidentifiedStore ?? throw new ArgumentNullException(nameof(unidentifiedStore));
    private readonly IEvaSubmissionQueries evaSubmissionQueries =
        evaSubmissionQueries ?? throw new ArgumentNullException(nameof(evaSubmissionQueries));
    private readonly IStaffAccountQueries staffAccounts =
        staffAccounts ?? throw new ArgumentNullException(nameof(staffAccounts));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    private IReadOnlyDictionary<Guid, string> jobCreatorNames = new Dictionary<Guid, string>();

    /// <summary>
    /// When this list was last read. Set only after the query returns, so a
    /// failed load never claims to be fresh (FRD-12).
    /// </summary>
    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public RequestOperationsProjection Operations { get; private set; } = new(
        ImmutableArray<RequestOperationProjection>.Empty,
        LimitReached: false);

    /// <summary>
    /// The AI Job List (FRD-27): every non-terminal job, plus the jobs that
    /// reached a terminal state today, newest first.
    /// </summary>
    public IReadOnlyList<AiJobRecord> AiJobs { get; private set; } = [];

    public EvaSubmissionActivity EvaActivity { get; private set; } = new(null);

    public IReadOnlyList<EvaSubmissionFailure> EvaFailures { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var nowUtc = timeProvider.GetUtcNow();
        // These projections each use an independent factory-created context.
        // Capture the instant once, then let their unrelated reads overlap.
        var operationsTask = getRequestOperations.ExecuteAsync(
            actor,
            asOfUtc: nowUtc,
            cancellationToken: cancellationToken);
        var evaActivityTask = evaSubmissionQueries.GetActivityAsync(cancellationToken);
        var evaFailuresTask = evaSubmissionQueries.GetRecentFailuresAsync(
            nowUtc - ServiceHealthPolicy.EvaRecentFailureWindow,
            ServiceHealthPolicy.MaximumEvaFailures,
            cancellationToken);
        var aiJobsTask = ReadAiJobsAsync(nowUtc, cancellationToken);
        await Task.WhenAll(operationsTask, evaActivityTask, evaFailuresTask, aiJobsTask);
        Operations = operationsTask.Result;
        EvaActivity = evaActivityTask.Result;
        EvaFailures = evaFailuresTask.Result;
        AiJobs = aiJobsTask.Result;
        FailedIntake = await ReadFailedIntakeAsync(actor, cancellationToken);
        // Started by is a name, never a stored subject id: the ledger keeps the
        // raw actor, so the usernames are resolved once for the whole list.
        jobCreatorNames = await ActorDisplayNames.ResolveStaffNamesAsync(
            staffAccounts,
            AiJobActions.StaffCreatorIds(AiJobs),
            cancellationToken);
        LoadedAtUtc = nowUtc;
        return Page();
    }

    public async Task<IActionResult> OnPostSendUnidentifiedToAiAsync(
        string unidentifiedReference,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid
            || !UnidentifiedReferenceFormat.TryParse(unidentifiedReference, out _))
        {
            StatusMessage = "The AI job request was invalid. Refresh and try again.";
            return RedirectToPage();
        }

        var unidentified = await unidentifiedStore.GetByReferenceAsync(
            unidentifiedReference.Trim(),
            cancellationToken);
        if (unidentified is not { State: UnidentifiedState.Open })
        {
            StatusMessage = "The Unidentified item was not found. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            await createAiJob.ExecuteAsync(
                new(
                    AiJobKind.UnidentifiedResolution,
                    unidentified.Id,
                    SubjectReference: null,
                    UnidentifiedInstruction,
                    TargetPercentOfEngineerValue: null,
                    actor,
                    operationKey),
                cancellationToken);
            StatusMessage = "The Unidentified item was sent to AI.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            StatusMessage = "The Unidentified item was not found. Refresh and try again.";
        }
        catch (ArgumentException)
        {
            StatusMessage = "The AI job request was invalid. Refresh and try again.";
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "AI work is not accepting new jobs.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCompleteAiJobAsync(
        Guid jobId,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || jobId == Guid.Empty)
        {
            StatusMessage = "The AI job could not be completed. Refresh and try again.";
            return RedirectToPage();
        }

        var job = (await aiJobQueries.ListOpenAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.JobId == jobId);
        if (job is null || !CanCompleteByHand(job))
        {
            StatusMessage = "The AI job could not be completed. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            await confirmAiJob.ExecuteAsync(
                new(jobId, expectedVersion, actor, operationKey),
                cancellationToken);
            StatusMessage = "The AI job was completed.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException
                or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            StatusMessage = "The AI job changed before it could be completed. Refresh and try again.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelAiJobAsync(
        Guid jobId,
        long expectedVersion,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || jobId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            StatusMessage = "The AI job could not be cancelled. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            await cancelAiJob.ExecuteAsync(
                new(jobId, expectedVersion, actor, operationKey, reason.Trim()),
                cancellationToken);
            StatusMessage = "The AI job was cancelled.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException
                or KeyNotFoundException or DbUpdateConcurrencyException)
        {
            StatusMessage = "The AI job changed before it could be cancelled. Refresh and try again.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRetryExternalAsync(
        Guid workItemId,
        int expectedAttemptCount,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || workItemId == Guid.Empty)
        {
            StatusMessage = "The external work retry request was invalid. Refresh and try again.";
            return RedirectToPage();
        }

        try
        {
            var result = await retryExternalWork.ExecuteAsync(
                new(workItemId, expectedAttemptCount, actor, operationKey),
                cancellationToken);
            StatusMessage = result.IsReplay
                ? "External work was already scheduled for retry."
                : "External work was scheduled for retry.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            StatusMessage = "The external work retry request was invalid. Refresh and try again.";
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "The external work failure changed before retry. Refresh and try again.";
        }

        return RedirectToPage();
    }


    /// <summary>
    /// The record page a job's subject opens, through the one map this list
    /// shares with the Action Logs reference column
    /// (<see cref="AiJobActions.RecordPage"/>).
    /// </summary>
    public static string? RecordPage(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return AiJobActions.RecordPage(job.SubjectKind);
    }

    /// <summary>
    /// Who started one job: a staff username, the connector client name, or
    /// Pegasus itself &#8212; never the stored subject identifier (FRD-27
    /// &#167; AI Job List).
    /// </summary>
    public string StartedBy(AiJobRecord job) => AiJobActions.StartedBy(
        job,
        jobCreatorNames,
        HttpContext.RequestServices.GetService<AutomationMcpOptions>()?.ClientId);

    /// <summary>
    /// The review action a Draft ready job offers, as (label, route), or
    /// <see langword="null"/> where no route exists. The route is Core's
    /// <see cref="StaffNotificationPolicy.AiDraftRoute"/>, the one the Work
    /// Centre and the Case's Next action open (FRD-27): Review estimate at the
    /// Case's Repair Spec section, Open query at the message it answers (the
    /// Case's correspondence when the job names none), Review at the
    /// Unidentified item. The label is the Work Centre's for the same draft
    /// action.
    /// </summary>
    public static (string Label, string Route)? ReviewAction(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.State == AiJobState.DraftReady
            && StaffNotificationPolicy.AiDraftRoute(job) is { } route
                ? (OperatorLabels.WorkCentre.AiDraftAction(AiDraftPolicy.ActionFor(job.Kind).ToString()), route)
                : null;
    }

    /// <summary>
    /// Whether staff close this job by hand. FRD-27 gives Complete job to a
    /// Draft ready Query response, Unidentified-queue pass or Market research;
    /// an Estimate and an Unidentified resolution are completed by the record's
    /// own act (Use estimate, Resolve destination), never from this table.
    /// </summary>
    public static bool CanCompleteByHand(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.State == AiJobState.DraftReady
            && job.Kind is AiJobKind.QueryResponse
                or AiJobKind.UnidentifiedQueuePass
                or AiJobKind.MarketResearch;
    }

    /// <summary>
    /// FRD-27's AI Job List membership: every non-terminal job, plus the jobs
    /// that reached a terminal state today, newest first.
    /// </summary>
    /// <remarks>
    /// Non-terminal membership comes from the persisted-open query. That query can also return a
    /// persisted Queued row whose effective state is Expired. Its terminal
    /// instant is <see cref="AiJobRecord.ExpiresAtUtc"/>, because expiry is
    /// derived at read time and does not write <see cref="AiJobRecord.ClosedAtUtc"/>.
    /// </remarks>
    private async Task<IReadOnlyList<AiJobRecord>> ReadAiJobsAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var (dayStartUtc, dayEndUtc, _) = LondonCalendar.DayAndWeekBoundariesAt(nowUtc);
        var open = await aiJobQueries.ListOpenAsync(cancellationToken);
        var terminal = await aiJobQueries.ListTerminalInWindowAsync(
            dayStartUtc, dayEndUtc, cancellationToken);
        return open
            .Where(job => !AiJobStates.IsTerminal(job.State)
                || ReachedTerminalToday(job, dayStartUtc, dayEndUtc))
            .Concat(terminal.Where(job => ReachedTerminalToday(job, dayStartUtc, dayEndUtc)))
            .DistinctBy(job => job.JobId)
            .OrderByDescending(job => job.CreatedAtUtc)
            .ThenByDescending(job => job.JobId)
            .ToArray();
    }

    private async Task<IReadOnlyList<IntakeLogActionableFailure>> ReadFailedIntakeAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ViewOperationalReports))
        {
            return [];
        }

        return await listIntakeLog.ListRetryableFailuresAsync(actor, cancellationToken);
    }

    private static bool ReachedTerminalToday(
        AiJobRecord job,
        DateTimeOffset dayStartUtc,
        DateTimeOffset dayEndUtc)
    {
        if (!AiJobStates.IsTerminal(job.State))
        {
            return false;
        }

        var terminalAtUtc = job.State == AiJobState.Expired
            ? job.ExpiresAtUtc
            : job.ClosedAtUtc;
        return terminalAtUtc is { } terminalAt
            && terminalAt >= dayStartUtc && terminalAt < dayEndUtc;
    }

}
