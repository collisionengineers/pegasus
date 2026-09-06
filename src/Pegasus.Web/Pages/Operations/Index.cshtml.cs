using System.Collections.Immutable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Operations;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ValidateAntiForgeryToken]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class IndexModel(
    GetRequestOperations getRequestOperations,
    RetryExternalWork retryExternalWork,
    IAcquireCaseEditLease acquireCaseEditLease,
    IReleaseCaseEditLease releaseCaseEditLease,
    IRevokeRequestUploadLink revokeRequestUploadLink,
    IAiJobQueries aiJobQueries,
    ICreateAiJob createAiJob,
    IConfirmAiJob confirmAiJob,
    ICancelAiJob cancelAiJob,
    IUnidentifiedStore unidentifiedStore,
    IEvaSubmissionQueries evaSubmissionQueries,
    TimeProvider timeProvider) : StaffPageModel
{
    private const string PreservedReasonKey = "OperationsRequestReason";
    private const string PreservedRequestIdKey = "OperationsRequestReasonId";

    /// <summary>
    /// How far back the list reaches for the terminal jobs of the current day
    /// (FRD-11 &#167; AI Job List). Non-terminal jobs never depend on this
    /// bound: they come from the unbounded <see cref="IAiJobQueries.ListOpenAsync"/>.
    /// </summary>
    private const int RecentJobWindow = 200;

    /// <summary>
    /// What one Unidentified-resolution job is asked to do. FRD-11 gives this
    /// kind "the U reference only" as its input, so the direction is fixed
    /// rather than typed: it is the pointer's payload, never operator copy.
    /// </summary>
    private const string UnidentifiedInstruction =
        "Propose a destination for this Unidentified item and give the reason.";

    private readonly GetRequestOperations getRequestOperations =
        getRequestOperations ?? throw new ArgumentNullException(nameof(getRequestOperations));
    private readonly RetryExternalWork retryExternalWork =
        retryExternalWork ?? throw new ArgumentNullException(nameof(retryExternalWork));
    private readonly IAcquireCaseEditLease acquireCaseEditLease =
        acquireCaseEditLease ?? throw new ArgumentNullException(nameof(acquireCaseEditLease));
    private readonly IReleaseCaseEditLease releaseCaseEditLease =
        releaseCaseEditLease ?? throw new ArgumentNullException(nameof(releaseCaseEditLease));
    private readonly IRevokeRequestUploadLink revokeRequestUploadLink =
        revokeRequestUploadLink ?? throw new ArgumentNullException(nameof(revokeRequestUploadLink));
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
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>
    /// When this list was last read. Set only after the query returns, so a
    /// failed load never claims to be fresh (FRD-12).
    /// </summary>
    public DateTimeOffset? LoadedAtUtc { get; private set; }

    public RequestOperationsProjection Operations { get; private set; } = new(
        ImmutableArray<RequestOperationProjection>.Empty,
        LimitReached: false);

    /// <summary>
    /// The AI Job List (FRD-11): every non-terminal job, plus the jobs that
    /// reached a terminal state today, newest first.
    /// </summary>
    public IReadOnlyList<AiJobRecord> AiJobs { get; private set; } = [];

    public EvaSubmissionActivity EvaActivity { get; private set; } = new(0, null);

    public IReadOnlyList<EvaSubmissionFailure> EvaFailures { get; private set; } = [];

    public Guid? PreservedRequestId { get; private set; }
    public string? PreservedReason { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        Operations = await getRequestOperations.ExecuteAsync(actor, cancellationToken);
        PreservedRequestId = ReadGuidTempData(PreservedRequestIdKey);
        PreservedReason = TempData[PreservedReasonKey] as string;
        var nowUtc = timeProvider.GetUtcNow();
        EvaActivity = await evaSubmissionQueries.GetActivityAsync(cancellationToken);
        EvaFailures = await evaSubmissionQueries.GetRecentFailuresAsync(
            nowUtc - ServiceHealthPolicy.EvaRecentFailureWindow,
            ServiceHealthPolicy.MaximumEvaFailures,
            cancellationToken);
        AiJobs = await ReadAiJobsAsync(nowUtc, cancellationToken);
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

    public async Task<IActionResult> OnPostRevokeLinkAsync(
        Guid requestId,
        Guid caseId,
        long expectedVersion,
        long expectedCaseVersion,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!ModelState.IsValid || requestId == Guid.Empty || caseId == Guid.Empty)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The link could not be withdrawn. Refresh and try again.";
            return RedirectToPage();
        }

        var leaseOperationKey = NewOperationKey();
        CaseEditLease lease;
        try
        {
            lease = await acquireCaseEditLease.ExecuteAsync(
                new(caseId, expectedCaseVersion, actor, leaseOperationKey),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "This link's case is open for editing by someone else.";
            return RedirectToPage();
        }

        try
        {
            await revokeRequestUploadLink.ExecuteAsync(
                new(caseId, requestId, actor, reason, operationKey, expectedVersion, expectedCaseVersion, lease.Token),
                cancellationToken);
            StatusMessage = "The link was withdrawn.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
            PreserveReason(requestId, reason);
            StatusMessage = "The link changed before it could be withdrawn. Refresh and try again.";
            await ReleaseQuietlyAsync(caseId, actor, lease.Token, cancellationToken);
            return RedirectToPage();
        }

        await ReleaseQuietlyAsync(caseId, actor, lease.Token, cancellationToken);
        return RedirectToPage();
    }

    public static string StateLabel(RequestOperationState state) =>
        Presentation.OperatorLabels.RequestOperationState(state);

    /// <summary>
    /// The record page a job's subject opens, or <see langword="null"/> when
    /// the job names no record. A queue pass is the only such kind: its
    /// subject is the Unidentified queue itself.
    /// </summary>
    public static string? RecordPage(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.SubjectKind switch
        {
            AiJobSubjectKind.Case => "/Cases/Details",
            AiJobSubjectKind.Unidentified => "/Unidentified/Details",
            _ => null
        };
    }

    /// <summary>
    /// The review action a Draft ready job offers, as (label, page), or
    /// <see langword="null"/> where no route exists. Estimate opens the
    /// Assessment estimate tab and Unidentified resolution opens the item, as
    /// FRD-11 requires.
    /// </summary>
    /// <remarks>
    /// Query response is the one compromise: FRD-11 asks it to open the
    /// message, but Core gives the job a Case subject and no message identity
    /// (<c>AiJobPolicy.SubjectKindFor</c>), so the link opens the Case the job
    /// actually names rather than rendering an unresolvable control.
    /// </remarks>
    public static (string Label, string Page)? ReviewAction(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.State != AiJobState.DraftReady || job.SubjectId is null)
        {
            return null;
        }

        return job.Kind switch
        {
            AiJobKind.Estimate =>
                (OperatorLabels.AiJobs.ReviewEstimate, "/Cases/Assessment/Index"),
            AiJobKind.QueryResponse =>
                (OperatorLabels.AiJobs.OpenQuery, "/Cases/Details"),
            AiJobKind.UnidentifiedResolution =>
                (OperatorLabels.AiJobs.Review, "/Unidentified/Details"),
            _ => null
        };
    }

    /// <summary>
    /// Whether staff close this job by hand. FRD-11 gives Complete job to a
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
    /// FRD-11's AI Job List membership: every non-terminal job, plus the jobs
    /// that reached a terminal state today, newest first.
    /// </summary>
    /// <remarks>
    /// Non-terminal membership comes from the unbounded persisted-open query,
    /// so no live job can fall outside <see cref="RecentJobWindow"/>; the
    /// window bounds only the terminal tail. That open query can also return a
    /// persisted Queued row whose effective state is Expired. Its terminal
    /// instant is <see cref="AiJobRecord.ExpiresAtUtc"/>, because expiry is
    /// derived at read time and does not write <see cref="AiJobRecord.ClosedAtUtc"/>.
    /// </remarks>
    private async Task<IReadOnlyList<AiJobRecord>> ReadAiJobsAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var today = OperatorLabels.OfficeDate(nowUtc);
        var open = await aiJobQueries.ListOpenAsync(cancellationToken);
        var recent = await aiJobQueries.ListRecentAsync(RecentJobWindow, cancellationToken);
        return open
            .Where(job => !AiJobStates.IsTerminal(job.State) || ReachedTerminalToday(job, today))
            .Concat(recent.Where(job => ReachedTerminalToday(job, today)))
            .DistinctBy(job => job.JobId)
            .OrderByDescending(job => job.CreatedAtUtc)
            .ThenByDescending(job => job.JobId)
            .ToArray();
    }

    private static bool ReachedTerminalToday(AiJobRecord job, string today)
    {
        if (!AiJobStates.IsTerminal(job.State))
        {
            return false;
        }

        var terminalAtUtc = job.State == AiJobState.Expired
            ? job.ExpiresAtUtc
            : job.ClosedAtUtc;
        return terminalAtUtc is { } terminalAt
            && OperatorLabels.OfficeDate(terminalAt) == today;
    }

    private async Task ReleaseQuietlyAsync(
        Guid caseId,
        ActionActor actor,
        string leaseToken,
        CancellationToken cancellationToken)
    {
        try
        {
            await releaseCaseEditLease.ExecuteAsync(
                new(caseId, actor, NewOperationKey(), leaseToken),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateConcurrencyException)
        {
        }

    }

    private Guid? ReadGuidTempData(string key, bool peek = false)
    {
        var value = peek ? TempData.Peek(key) : TempData[key];
        return value switch
        {
            Guid parsed => parsed,
            string text when Guid.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    private void PreserveReason(Guid requestId, string? reason)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        var normalized = reason.Trim();
        if (normalized.Length > 500)
        {
            return;
        }

        TempData[PreservedRequestIdKey] = requestId.ToString("D");
        TempData[PreservedReasonKey] = normalized;
    }

}
