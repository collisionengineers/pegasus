using Pegasus.Core.AiWork;
using Pegasus.Core.Custody;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

/// <summary>
/// The Service health table (FRD-12 § Operations; EPIC-011 §1.11). Every row
/// is derived from a fact the system already recorded — a poll cursor, a
/// work item, a submission attempt, a job, a switch — and names the time of
/// that evidence. Nothing here probes a dependency, and a service with no
/// composed source has no row rather than a guessed one.
/// </summary>
public enum ServiceHealthArea
{
    Mail,
    Intake,
    Custody,
    Eva,
    Ai,
    Automation
}

public enum ServiceHealthState
{
    /// <summary>The latest evidence is recent and reports no failure.</summary>
    Current,

    /// <summary>The service works but its evidence is stale or some items are backing off.</summary>
    Partial,

    /// <summary>The latest recorded outcome is a failure.</summary>
    Failed,

    /// <summary>Work is queued or in flight and nothing has failed.</summary>
    Running,

    /// <summary>The service is set up (or deliberately switched off) and has no evidence yet.</summary>
    Configured,

    /// <summary>A recorded outcome is waiting for a person's decision.</summary>
    ReviewRequired
}

public enum ServiceHealthDependency
{
    MicrosoftGraph,
    Worker,
    Box,
    EvaApi,
    AiConnector,
    AutomationClient
}

/// <summary>
/// Exactly the identity <see cref="RetryExternalWorkCommand"/> needs; the
/// Retry column is that command and nothing else.
/// </summary>
public sealed record ServiceHealthRetryTarget(Guid WorkItemId, int ExpectedAttemptCount);

public sealed record ServiceHealthRow(
    ServiceHealthArea Area,
    string Service,
    ServiceHealthState State,
    DateTimeOffset? LatestEvidenceAtUtc,
    ServiceHealthDependency Dependency,
    ServiceHealthRetryTarget? RetryTarget = null,
    string? FailureCode = null);

/// <summary>
/// <see cref="ExternalWorkLimitReached"/> is the Operations projection's own
/// flag: when set, the Custody rows describe only the first
/// <see cref="GetRequestOperations.MaximumItems"/> items and the page must
/// say so rather than present the rows as the whole queue.
/// </summary>
public sealed record ServiceHealthSnapshot(
    DateTimeOffset AsOfUtc,
    IReadOnlyList<ServiceHealthRow> Rows,
    bool ExternalWorkLimitReached);

/// <summary>
/// What the Sent-items evidence poll has managed for one mailbox: the same
/// four facts the inbound poll records, read from the sent-poll cursor row.
/// </summary>
public sealed record SentEvidencePollStatus(
    string MailboxAddress,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    string? LastFailureCode);

/// <summary>
/// The queued-intake dispatcher by state: <see cref="Active"/> is pending,
/// dispatching, dispatched or processing; the other two are the states that
/// carry a recorded failure.
/// </summary>
public sealed record IntakeDispatchHealth(
    int Active,
    int RetryScheduled,
    int Failed,
    DateTimeOffset? LatestCompletedAtUtc);

/// <summary>
/// The two health facts no existing port owns. Everything else the snapshot
/// shows is read through the port that already owns it.
/// </summary>
public interface IServiceHealthQueries
{
    Task<IReadOnlyList<SentEvidencePollStatus>> ListSentEvidencePollStatusAsync(
        CancellationToken cancellationToken);

    Task<IntakeDispatchHealth> GetIntakeDispatchHealthAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Whether the Automation client may currently obtain tokens. The switch
/// itself lives with the token endpoint in the Web host; Core only reads it.
/// </summary>
public interface IAutomationIngressStatusQueries
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of how a recorded fact becomes a health state.
/// </summary>
public static class ServiceHealthPolicy
{
    /// <summary>
    /// How far back an EVA failure still counts as recent on the health row.
    /// A failure older than this is still on the case; it has just stopped
    /// being a service-level signal.
    /// </summary>
    public static readonly TimeSpan EvaRecentFailureWindow = TimeSpan.FromDays(1);

    public const int MaximumEvaFailures = 20;

    public const string SentEvidenceService = "Sent evidence";
    public const string IntakeDispatchService = "Intake dispatch";
    public const string ExternalWorkService = "External work";
    public const string EvaService = "EVA submissions";
    public const string AiJobsService = "AI jobs";
    public const string AutomationService = "Automation ingress";

    public static bool HasPartialData(ServiceHealthSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Rows.Any(row =>
            row.State is ServiceHealthState.Partial or ServiceHealthState.Failed);
    }

    /// <summary>
    /// A poll cursor: a recorded failure code wins, a cursor that has never
    /// completed has no evidence, and a completed poll goes stale at the
    /// same age the Inbox stops calling itself current.
    /// </summary>
    public static ServiceHealthState PollState(
        DateTimeOffset? lastCompletedAtUtc,
        string? lastFailureCode,
        DateTimeOffset nowUtc)
    {
        if (!string.IsNullOrWhiteSpace(lastFailureCode))
        {
            return ServiceHealthState.Failed;
        }
        if (lastCompletedAtUtc is not { } completed)
        {
            return ServiceHealthState.Configured;
        }

        return nowUtc - completed > GetRetainedMailFreshness.StaleAfter
            ? ServiceHealthState.Partial
            : ServiceHealthState.Current;
    }

    public static ServiceHealthState DispatchState(IntakeDispatchHealth health)
    {
        ArgumentNullException.ThrowIfNull(health);
        if (health.Failed > 0)
        {
            return ServiceHealthState.Failed;
        }
        if (health.RetryScheduled > 0)
        {
            return ServiceHealthState.Partial;
        }

        if (health.Active > 0)
        {
            return ServiceHealthState.Running;
        }

        // An empty queue that has never completed anything has no evidence
        // to call itself current on.
        return health.LatestCompletedAtUtc is null
            ? ServiceHealthState.Configured
            : ServiceHealthState.Current;
    }

    /// <summary>
    /// A failed EVA attempt is never retried by the system (FRD-07), so it
    /// asks for a person rather than reporting the service down.
    /// </summary>
    public static ServiceHealthState EvaState(
        EvaSubmissionActivity activity,
        IReadOnlyList<EvaSubmissionFailure> recentFailures)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(recentFailures);
        if (recentFailures.Count > 0)
        {
            return ServiceHealthState.ReviewRequired;
        }
        if (activity.PendingWorkCount > 0)
        {
            return ServiceHealthState.Running;
        }

        return activity.LatestSubmittedAtUtc is null
            ? ServiceHealthState.Configured
            : ServiceHealthState.Current;
    }

    /// <summary>
    /// A switched-off Send to AI channel is a configured state, not a fault;
    /// a failed job is terminal and waits for a person (FRD-11 § AI Job List).
    /// </summary>
    public static ServiceHealthState AiState(
        bool sendToAiEnabled,
        AiJobCounts counts,
        DateTimeOffset? latestEvidenceAtUtc)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (!sendToAiEnabled)
        {
            return ServiceHealthState.Configured;
        }
        if (counts.Failed > 0)
        {
            return ServiceHealthState.ReviewRequired;
        }
        if (counts.Active > 0)
        {
            return ServiceHealthState.Running;
        }

        return latestEvidenceAtUtc is null
            ? ServiceHealthState.Configured
            : ServiceHealthState.Current;
    }

    public static ServiceHealthState AutomationState(bool enabled) =>
        enabled ? ServiceHealthState.Current : ServiceHealthState.Configured;

    public static ServiceHealthDependency ExternalWorkDependency(string? externalKind) =>
        externalKind switch
        {
            ExternalWorkKinds.CreateCaseCustody
                or ExternalWorkKinds.CreateAuditReferenceCustody
                or ExternalWorkKinds.CreateImageCaseCustody
                or ExternalWorkKinds.MergeImageCaseCustody => ServiceHealthDependency.Box,
            ExternalWorkKinds.SubmitCaseToEva => ServiceHealthDependency.EvaApi,
            _ => ServiceHealthDependency.Worker
        };

    /// <summary>
    /// The Custody/external-work rows from the Operations projection: one row
    /// per retryable failure carrying its retry identity, or a single row for
    /// the queue when nothing has failed.
    /// </summary>
    public static IReadOnlyList<ServiceHealthRow> ExternalWorkRows(
        IReadOnlyList<RequestOperationProjection> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var work = operations
            .Where(item => item.Kind == RequestOperationKind.ExternalWork)
            .ToList();
        var failed = work
            .Where(item => item.State == RequestOperationState.Failed && item.CanRetry)
            .OrderBy(item => item.LastActivityAtUtc)
            .ToList();
        if (failed.Count > 0)
        {
            return failed
                .Select(item => new ServiceHealthRow(
                    ServiceHealthArea.Custody,
                    item.ExternalKind ?? ExternalWorkService,
                    ServiceHealthState.Failed,
                    item.LastActivityAtUtc,
                    ExternalWorkDependency(item.ExternalKind),
                    new(item.Id, item.AttemptCount!.Value)))
                .ToList();
        }

        DateTimeOffset? latest = work.Count == 0
            ? null
            : work.Max(item => item.LastActivityAtUtc);
        var pending = work.Any(item => item.State == RequestOperationState.Pending);
        return
        [
            new(
                ServiceHealthArea.Custody,
                ExternalWorkService,
                latest is null
                    ? ServiceHealthState.Configured
                    : pending ? ServiceHealthState.Running : ServiceHealthState.Current,
                latest,
                ServiceHealthDependency.Worker)
        ];
    }
}

public sealed class GetServiceHealth(
    IApprovedMailboxPollStatusQueries mailboxPolls,
    IServiceHealthQueries healthQueries,
    GetRequestOperations requestOperations,
    IEvaSubmissionQueries evaSubmissions,
    IAiJobQueries aiJobs,
    ISendToAiControl sendToAiControl,
    IAutomationIngressStatusQueries automationIngress,
    IAutomationActivityQueries automationActivity,
    TimeProvider timeProvider)
{
    private readonly IApprovedMailboxPollStatusQueries mailboxPolls =
        mailboxPolls ?? throw new ArgumentNullException(nameof(mailboxPolls));
    private readonly IServiceHealthQueries healthQueries =
        healthQueries ?? throw new ArgumentNullException(nameof(healthQueries));
    private readonly GetRequestOperations requestOperations =
        requestOperations ?? throw new ArgumentNullException(nameof(requestOperations));
    private readonly IEvaSubmissionQueries evaSubmissions =
        evaSubmissions ?? throw new ArgumentNullException(nameof(evaSubmissions));
    private readonly IAiJobQueries aiJobs =
        aiJobs ?? throw new ArgumentNullException(nameof(aiJobs));
    private readonly ISendToAiControl sendToAiControl =
        sendToAiControl ?? throw new ArgumentNullException(nameof(sendToAiControl));
    private readonly IAutomationIngressStatusQueries automationIngress =
        automationIngress ?? throw new ArgumentNullException(nameof(automationIngress));
    private readonly IAutomationActivityQueries automationActivity =
        automationActivity ?? throw new ArgumentNullException(nameof(automationActivity));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<ServiceHealthSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var nowUtc = timeProvider.GetUtcNow();
        var rows = new List<ServiceHealthRow>();

        foreach (var poll in await mailboxPolls.ListAsync(cancellationToken))
        {
            rows.Add(new(
                ServiceHealthArea.Mail,
                poll.MailboxAddress,
                ServiceHealthPolicy.PollState(poll.LastCompletedAtUtc, poll.LastFailureCode, nowUtc),
                poll.LastCompletedAtUtc,
                ServiceHealthDependency.MicrosoftGraph,
                FailureCode: poll.LastFailureCode));
        }

        foreach (var poll in await healthQueries.ListSentEvidencePollStatusAsync(cancellationToken))
        {
            rows.Add(new(
                ServiceHealthArea.Mail,
                $"{ServiceHealthPolicy.SentEvidenceService} · {poll.MailboxAddress}",
                ServiceHealthPolicy.PollState(poll.LastCompletedAtUtc, poll.LastFailureCode, nowUtc),
                poll.LastCompletedAtUtc,
                ServiceHealthDependency.MicrosoftGraph,
                FailureCode: poll.LastFailureCode));
        }

        var dispatch = await healthQueries.GetIntakeDispatchHealthAsync(cancellationToken);
        rows.Add(new(
            ServiceHealthArea.Intake,
            ServiceHealthPolicy.IntakeDispatchService,
            ServiceHealthPolicy.DispatchState(dispatch),
            dispatch.LatestCompletedAtUtc,
            ServiceHealthDependency.Worker));

        var operations = await requestOperations.ExecuteAsync(actor, cancellationToken);
        rows.AddRange(ServiceHealthPolicy.ExternalWorkRows(operations.Items));

        var evaActivity = await evaSubmissions.GetActivityAsync(cancellationToken);
        var evaFailures = await evaSubmissions.GetRecentFailuresAsync(
            nowUtc - ServiceHealthPolicy.EvaRecentFailureWindow,
            ServiceHealthPolicy.MaximumEvaFailures,
            cancellationToken);
        rows.Add(new(
            ServiceHealthArea.Eva,
            ServiceHealthPolicy.EvaService,
            ServiceHealthPolicy.EvaState(evaActivity, evaFailures),
            evaActivity.LatestSubmittedAtUtc,
            ServiceHealthDependency.EvaApi));

        var aiCounts = await aiJobs.GetCountsAsync(cancellationToken);
        var recentJobs = await aiJobs.ListRecentAsync(1, cancellationToken);
        DateTimeOffset? aiEvidence = recentJobs.Count == 0
            ? null
            : recentJobs[0].ClosedAtUtc is { } closed && closed > recentJobs[0].CreatedAtUtc
                ? closed
                : recentJobs[0].CreatedAtUtc;
        var sendToAiEnabled = await sendToAiControl.IsEnabledAsync(cancellationToken);
        rows.Add(new(
            ServiceHealthArea.Ai,
            ServiceHealthPolicy.AiJobsService,
            ServiceHealthPolicy.AiState(sendToAiEnabled, aiCounts, aiEvidence),
            aiEvidence,
            ServiceHealthDependency.AiConnector));

        var ingressEnabled = await automationIngress.IsEnabledAsync(cancellationToken);
        // Read through the port, not ListAutomationActivity: that use case is
        // gated on ManageAutomationClients because it exposes the records
        // themselves, whereas this row exposes only the newest timestamp to a
        // PerformCasework reader. Nothing else from the record leaves here.
        var newestActivity = await automationActivity.ListAsync(
            new(actor, Page: 1, PageSize: 1),
            cancellationToken);
        rows.Add(new(
            ServiceHealthArea.Automation,
            ServiceHealthPolicy.AutomationService,
            ServiceHealthPolicy.AutomationState(ingressEnabled),
            newestActivity.Records.Count == 0 ? null : newestActivity.Records[0].OccurredAtUtc,
            ServiceHealthDependency.AutomationClient));

        return new(nowUtc, rows, operations.LimitReached);
    }
}
