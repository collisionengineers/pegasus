using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>One AI jobs row: the job and its draft (route and action) when Draft ready.</summary>
public sealed record WorkCentreAiJob(AiJobRecord Job, AiDraft? Draft);

public interface IListWorkCentreAiJobs
{
    /// <summary>
    /// The office's unfinished AI work (D9) from the open jobs the caller has
    /// read: Queued, Taken and Draft ready, and the jobs that failed within the
    /// New cases window, less those dismissed.
    /// </summary>
    Task<IReadOnlyList<WorkCentreAiJob>> ExecuteAsync(
        ActionActor actor,
        IReadOnlyList<AiJobRecord> openJobs,
        int aiDraftTargetDays,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken);
}

public sealed class ListWorkCentreAiJobs(
    IAiJobQueries aiJobs,
    IWorkCentreDismissalStore dismissals) : IListWorkCentreAiJobs
{
    /// <summary>How far back the section reads for Failed jobs; the window is the New cases window.</summary>
    public const int RecentJobWindow = 200;

    private readonly IAiJobQueries _aiJobs = aiJobs ?? throw new ArgumentNullException(nameof(aiJobs));
    private readonly IWorkCentreDismissalStore _dismissals =
        dismissals ?? throw new ArgumentNullException(nameof(dismissals));

    public async Task<IReadOnlyList<WorkCentreAiJob>> ExecuteAsync(
        ActionActor actor,
        IReadOnlyList<AiJobRecord> openJobs,
        int aiDraftTargetDays,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(openJobs);
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);

        var drafts = AiDraftPolicy.Drafts(openJobs, aiDraftTargetDays)
            .ToDictionary(draft => draft.Job.JobId);
        var windowStart = RecentCasesPolicy.WindowStart(asOfUtc);
        // Market research never waits for a person and is not listed.
        var failed = (await _aiJobs.ListRecentAsync(RecentJobWindow, cancellationToken))
            .Where(job => job.State == AiJobState.Failed && (job.ClosedAtUtc ?? job.CreatedAtUtc) >= windowStart);
        var jobs = openJobs
            .Where(job => job.State is AiJobState.Queued or AiJobState.Taken or AiJobState.DraftReady)
            .Concat(failed)
            .Where(job => job.Kind != AiJobKind.MarketResearch)
            .DistinctBy(job => job.JobId)
            .OrderBy(job => job.State switch
            {
                AiJobState.DraftReady => 0,
                AiJobState.Taken => 1,
                AiJobState.Queued => 2,
                _ => 3
            })
            .ThenByDescending(job => job.CreatedAtUtc)
            .ToArray();
        var shown = await WorkCentreDismissalPolicy.WithoutDismissedAsync(
            _dismissals,
            jobs,
            job => job.JobId,
            WorkCentreDismissalPolicy.AiJobQualifiedAt,
            cancellationToken);
        return shown
            .Select(job => new WorkCentreAiJob(job, drafts.GetValueOrDefault(job.JobId)))
            .ToArray();
    }
}
