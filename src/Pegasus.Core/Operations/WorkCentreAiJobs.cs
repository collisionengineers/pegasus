using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>
/// One AI actions row: the job, its draft (route and action) when Draft ready,
/// and the Engineer who took the job's estimate into the Case, when one did.
/// </summary>
public sealed record WorkCentreAiJob(AiJobRecord Job, AiDraft? Draft, Guid? AdoptedByStaffId = null);

/// <summary>
/// The AI actions sub-filters (v42 Design A): every AI action, the estimate
/// jobs, the query reply drafts, and the jobs that failed. A filter names what
/// the job is or what became of it, never how it is displayed.
/// </summary>
public enum WorkCentreAiActionFilter
{
    All,
    Estimate,
    QueryDraft,
    Failed
}

public static class WorkCentreAiActionPolicy
{
    public static bool Matches(WorkCentreAiActionFilter filter, AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return filter switch
        {
            WorkCentreAiActionFilter.Estimate => job.Kind == AiJobKind.Estimate,
            WorkCentreAiActionFilter.QueryDraft => job.Kind == AiJobKind.QueryResponse,
            WorkCentreAiActionFilter.Failed => job.State == AiJobState.Failed,
            _ => true
        };
    }
}

/// <summary>
/// The Engineer who took a job's estimate into the Case: the staff act that
/// set the Repair specification the job drafted as current. Read from the
/// records the act wrote, never inferred from the job.
/// </summary>
public interface IAiDraftAdoptionQueries
{
    /// <summary>The adopting staff id of each listed estimate job that one adopted.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> ReadAsync(
        IReadOnlyCollection<Guid> jobIds,
        CancellationToken cancellationToken);
}

public sealed class ListWorkCentreAiJobs(
    IAiJobQueries aiJobs,
    IAiDraftAdoptionQueries adoptions,
    IWorkCentreDismissalStore dismissals)
{
    /// <summary>How far back the section reads for Completed and Failed jobs; the window is the New cases window.</summary>
    public const int RecentJobWindow = 200;

    private readonly IAiJobQueries _aiJobs = aiJobs ?? throw new ArgumentNullException(nameof(aiJobs));
    private readonly IAiDraftAdoptionQueries _adoptions =
        adoptions ?? throw new ArgumentNullException(nameof(adoptions));
    private readonly IWorkCentreDismissalStore _dismissals =
        dismissals ?? throw new ArgumentNullException(nameof(dismissals));

    /// <summary>
    /// The office's unfinished AI work (D9) from the open jobs the caller has
    /// read: Queued, Taken and Draft ready, and the jobs that completed or
    /// failed within the New cases window, less those dismissed.
    /// <paramref name="dismissals"/> is the section's handle on a dismissal
    /// lookup shared with the attention list; absent, the section reads its own.
    /// </summary>
    public async Task<IReadOnlyList<WorkCentreAiJob>> ExecuteAsync(
        ActionActor actor,
        IReadOnlyList<AiJobRecord> openJobs,
        int aiDraftTargetDays,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken,
        IWorkCentreDismissalLookup? dismissals = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(openJobs);
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);

        var drafts = AiDraftPolicy.Drafts(openJobs, aiDraftTargetDays)
            .ToDictionary(draft => draft.Job.JobId);
        var windowStart = RecentCasesPolicy.WindowStart(asOfUtc);
        // Market research never waits for a person and is not listed.
        var closed = (await _aiJobs.ListRecentAsync(RecentJobWindow, cancellationToken))
            .Where(job => job.State is AiJobState.Completed or AiJobState.Failed
                && (job.ClosedAtUtc ?? job.CreatedAtUtc) >= windowStart);
        var jobs = openJobs
            .Where(job => job.State is AiJobState.Queued or AiJobState.Taken or AiJobState.DraftReady)
            .Concat(closed)
            .Where(job => job.Kind != AiJobKind.MarketResearch)
            .DistinctBy(job => job.JobId)
            .OrderBy(job => job.State switch
            {
                AiJobState.DraftReady => 0,
                AiJobState.Taken => 1,
                AiJobState.Queued => 2,
                AiJobState.Completed => 3,
                _ => 4
            })
            .ThenByDescending(job => job.CreatedAtUtc)
            .ToArray();
        var shown = await WorkCentreDismissalPolicy.WithoutDismissedAsync(
            dismissals ?? _dismissals,
            jobs,
            job => job.JobId,
            WorkCentreDismissalPolicy.AiJobQualifiedAt,
            cancellationToken);
        var adopted = await ReadAdoptedAsync(shown, cancellationToken);
        return shown
            .Select(job => new WorkCentreAiJob(
                job,
                drafts.GetValueOrDefault(job.JobId),
                adopted.TryGetValue(job.JobId, out var staffId) ? staffId : null))
            .ToArray();
    }

    /// <summary>One read for the estimate jobs on the list; other kinds adopt nothing.</summary>
    private async Task<IReadOnlyDictionary<Guid, Guid>> ReadAdoptedAsync(
        IReadOnlyList<AiJobRecord> jobs,
        CancellationToken cancellationToken)
    {
        var estimateJobIds = jobs
            .Where(job => job.Kind == AiJobKind.Estimate)
            .Select(job => job.JobId)
            .Distinct()
            .ToArray();
        return estimateJobIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await _adoptions.ReadAsync(estimateJobIds, cancellationToken);
    }
}
