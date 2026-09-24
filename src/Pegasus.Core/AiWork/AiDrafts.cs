using Pegasus.Core.Notifications;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.AiWork;

/// <summary>
/// The per-kind action a person takes on a Draft ready job (FRD-27, Work Centre
/// D9): Review estimate opens the Case's Repair Spec section, Open query opens
/// the message, Review opens the Unidentified item.
/// </summary>
public enum AiDraftAction
{
    ReviewEstimate,
    OpenQuery,
    Review
}

/// <summary>
/// A Draft ready job as the Case record's Next action panel and the Work Centre
/// see it: the job, where its action opens, when the draft was written and when
/// it is due under the AI draft target. Market research is never a draft.
/// </summary>
public sealed record AiDraft(
    AiJobRecord Job,
    AiDraftAction Action,
    string Route,
    DateTimeOffset DraftWrittenAtUtc,
    DateTimeOffset DueAtUtc);

public interface IAiDraftQueries
{
    /// <summary>The Case's Draft ready jobs, oldest draft first.</summary>
    Task<IReadOnlyList<AiDraft>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken);

    /// <summary>Every Draft ready job in the office, oldest draft first, for the Work Centre.</summary>
    Task<IReadOnlyList<AiDraft>> ListOpenAsync(CancellationToken cancellationToken);
}

public static class AiDraftPolicy
{
    /// <summary>A job waiting for a person's review. Market research never waits for one.</summary>
    public static bool IsDraft(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.State == AiJobState.DraftReady
            && job.Kind is AiJobKind.Estimate
                or AiJobKind.QueryResponse
                or AiJobKind.UnidentifiedResolution
                or AiJobKind.UnidentifiedQueuePass;
    }

    public static AiDraftAction ActionFor(AiJobKind kind) => kind switch
    {
        AiJobKind.Estimate => AiDraftAction.ReviewEstimate,
        AiJobKind.QueryResponse => AiDraftAction.OpenQuery,
        AiJobKind.UnidentifiedResolution or AiJobKind.UnidentifiedQueuePass => AiDraftAction.Review,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Market research is not a draft.")
    };

    /// <summary>
    /// The draft is due <paramref name="targetDays"/> calendar days after it was
    /// written, at midnight Europe/London: a target of 0 is the next midnight.
    /// </summary>
    public static DateTimeOffset DueAt(DateTimeOffset draftWrittenAtUtc, int targetDays) =>
        WorkTargets.DueAt(draftWrittenAtUtc, targetDays);

    public static AiDraft? ToDraft(AiJobRecord job, int targetDays)
    {
        if (!IsDraft(job))
        {
            return null;
        }

        var route = StaffNotificationPolicy.AiDraftRoute(job) ?? "/Operations";
        var written = job.DraftReadyAtUtc ?? job.TakenAtUtc ?? job.CreatedAtUtc;
        return new AiDraft(job, ActionFor(job.Kind), route, written, DueAt(written, targetDays));
    }
}

/// <summary>
/// The one rule for turning an event and a target into a due instant, shared by
/// every Work Centre kind: calendar days, the day boundary at midnight
/// Europe/London, a target of 0 meaning "by the next midnight after the event".
/// </summary>
public static class WorkTargets
{
    public static DateTimeOffset DueAt(DateTimeOffset eventAtUtc, int targetDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetDays);
        var day = LondonCalendar.DateAt(eventAtUtc);
        return LondonCalendar.StartOfDay(day.AddDays(targetDays + 1));
    }

    /// <summary>A hold's review date is due at the end of that Europe/London day.</summary>
    public static DateTimeOffset EndOfDay(DateOnly date) =>
        LondonCalendar.StartOfDay(date.AddDays(1));
}

public sealed class AiDraftQueries(
    IAiJobQueries jobs,
    ICaseWorkflowConfiguration configuration) : IAiDraftQueries
{
    private readonly IAiJobQueries _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
    private readonly ICaseWorkflowConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<IReadOnlyList<AiDraft>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        return Drafts(
            await _jobs.ListForSubjectAsync(caseId, cancellationToken),
            (await _configuration.GetCurrentAsync(cancellationToken)).AiDraftTargetDays);
    }

    public async Task<IReadOnlyList<AiDraft>> ListOpenAsync(CancellationToken cancellationToken) =>
        Drafts(
            await _jobs.ListOpenAsync(cancellationToken),
            (await _configuration.GetCurrentAsync(cancellationToken)).AiDraftTargetDays);

    private static AiDraft[] Drafts(IEnumerable<AiJobRecord> jobs, int targetDays) =>
        jobs.Select(job => AiDraftPolicy.ToDraft(job, targetDays))
            .OfType<AiDraft>()
            .OrderBy(draft => draft.DraftWrittenAtUtc)
            .ThenBy(draft => draft.Job.JobId)
            .ToArray();
}
