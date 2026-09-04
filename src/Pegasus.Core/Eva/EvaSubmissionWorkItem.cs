using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Eva;

/// <summary>
/// Mirrors <see cref="Vehicle.VehicleLookupWorkState"/> so an operator reading
/// two durable queues sees one vocabulary.
/// </summary>
public enum EvaSubmissionWorkState
{
    Pending,
    Processing,
    RetryScheduled,
    Completed,
    Failed,
    Poisoned
}

public sealed record EvaSubmissionWorkItem(
    Guid Id,
    Guid CaseId,
    string OperationKey,
    EvaSubmissionWorkState State,
    int AttemptCount,
    string? LeaseToken);

public interface IEvaSubmissionWorkStore
{
    Task<EvaSubmissionWorkItem?> ClaimProcessingAsync(
        Guid workItemId,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task RecordOutcomeAsync(
        Guid workItemId,
        string leaseToken,
        EvaSubmissionWorkState state,
        string? failureCode,
        string? failureReason,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken);
}

public interface IAutomaticEvaSubmissionStore
{
    Task<int> EnqueueDueAsync(int maximumItems, CancellationToken cancellationToken);
}

/// <summary>
/// EXT-04: enqueues an EVA submission for every case sitting in Review whose
/// principal has automatic submission switched on and which has not already
/// been submitted.
///
/// A sweep rather than a hook, and deliberately so. Three separate places
/// write <c>State = Review</c> — staff confirming completeness, the explicit
/// return-to-review action, and the Worker confirming custody of a definitive
/// intake — and each does it inside its own serializable transaction. Adding a
/// fourth write to all three would put an EVA concern inside three unrelated
/// commits and give three chances to miss one. Sweeping instead means one
/// insertion point, and it self-heals: a case whose enqueue was lost is picked
/// up on the next pass rather than never.
///
/// It is idempotent per case. The durable row is the marker, so a case already
/// queued, already submitted, or already refused is not queued again.
/// </summary>
public sealed class ReconcileAutomaticEvaSubmissions(IAutomaticEvaSubmissionStore store)
{
    private readonly IAutomaticEvaSubmissionStore store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<int> ExecuteAsync(int maximumItems, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        return store.EnqueueDueAsync(maximumItems, cancellationToken);
    }
}

/// <summary>
/// EXT-04: the automatic submission of one case, taken off the durable queue.
///
/// Shaped like <see cref="Vehicle.ProcessQueuedVehicleLookup"/> — claim a
/// lease, do the work, record the outcome, schedule a retry or stop — because
/// it is the same problem and an operator watching the Operations page should
/// not have to learn a second set of states.
///
/// The submission itself is the same act the button performs, through the same
/// <see cref="ISubmitCaseToEva"/>. Only the actor and the trigger differ: this
/// runs as the Worker, which holds
/// <see cref="StaffAccessRight.ExecuteSystemWork"/> rather than casework
/// rights.
///
/// What it must never do is retry an outcome it already knows the answer to.
/// EVA has no idempotency, so a blind resend creates a second claim; only an
/// <see cref="EvaSubmissionOutcome.Unknown"/> outcome is rescheduled, and
/// <see cref="EvaSubmissionRetryPolicy"/> owns how often.
/// </summary>
public sealed class ProcessQueuedEvaSubmission(
    IEvaSubmissionWorkStore workStore,
    ISubmitCaseToEva submitCaseToEva,
    TimeProvider timeProvider) : IProcessQueuedEvaSubmission
{
    /// <summary>
    /// Long enough to read every photograph of a case out of Box and push them
    /// to EVA. The transport's own timeout is 100 seconds per request; this is
    /// the outer bound on the whole attempt.
    /// </summary>
    private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);

    public const string WorkerActorId = "eva-automatic-submission";

    public async Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException(
                "An EVA submission work item identifier is required.",
                nameof(workItemId));
        }

        var nowUtc = timeProvider.GetUtcNow();
        var workItem = await workStore.ClaimProcessingAsync(
            workItemId,
            nowUtc,
            ProcessingLease,
            cancellationToken);

        // Another worker holds the lease, or the row is already terminal.
        // Neither is an error and neither is ours to finish.
        if (workItem is null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(workItem.LeaseToken))
        {
            throw new InvalidOperationException(
                "Claimed EVA submission work has no processing lease.");
        }
        if (workItem.AttemptCount < 1)
        {
            throw new InvalidOperationException(
                "Claimed EVA submission work has an invalid attempt count.");
        }

        SubmitCaseToEvaResult? result;
        try
        {
            result = await submitCaseToEva.ExecuteAsync(
                new(
                    workItem.CaseId,
                    ActionActor.SystemWorker(WorkerActorId),
                    EvaSubmissionPolicy.AttemptOperationKey(
                        workItem.OperationKey,
                        workItem.AttemptCount),
                    EvaSubmissionTrigger.Automatic),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // The case left Review, its principal switched the route off, or it
        // has no eligible signatory. These are answers, not faults: the work is
        // finished and must not be retried into existence.
        catch (Exception exception) when (exception is EvaHandoffStateException
            or EvaSubmissionNotEnabledException
            or EvaSignOffEngineerRequiredException)
        {
            await RecordAsync(
                workItem,
                EvaSubmissionWorkState.Completed,
                "eva_submission_no_longer_applicable",
                exception.Message,
                dueAtUtc: null,
                nowUtc,
                cancellationToken);
            return;
        }
        // A dependency failed before EVA was reached — Box could not be read,
        // the database was unavailable. Nothing was sent, so retrying is safe
        // and is the only branch here where that is true.
        //
        // No HttpRequestException here on purpose: naming it would make Core
        // depend on System.Net.Http, which the architecture test forbids and
        // which would be the wrong shape anyway. The EVA call cannot throw it
        // (the transport returns an Unknown outcome instead), and the custody
        // read's transport failure is translated to IOException at the
        // Infrastructure boundary that raises it.
        catch (Exception exception) when (exception is IOException
            or InvalidDataException
            or TimeoutException
            or UnauthorizedAccessException)
        {
            await RescheduleOrFailAsync(
                workItem,
                EvaSubmissionOutcome.Unknown,
                "eva_submission_dependency_failure",
                exception.Message,
                nowUtc,
                cancellationToken);
            return;
        }

        if (result is null)
        {
            await RecordAsync(
                workItem,
                EvaSubmissionWorkState.Failed,
                "eva_submission_case_missing",
                null,
                dueAtUtc: null,
                nowUtc,
                cancellationToken);
            return;
        }

        // Nothing was sent because the case is not sendable — no photographs,
        // most often. Terminal: a sweep will pick the case up again when it
        // next qualifies, and burning retries on it changes nothing.
        if (result.Submission is not { } submission)
        {
            await RecordAsync(
                workItem,
                EvaSubmissionWorkState.Failed,
                "eva_submission_blocked",
                result.BlockingReasons.Count > 0
                    ? string.Join(" ", result.BlockingReasons)
                    : null,
                dueAtUtc: null,
                nowUtc,
                cancellationToken);
            return;
        }

        if (EvaSubmissionPolicy.IsRetryable(submission.Outcome))
        {
            await RescheduleOrFailAsync(
                workItem,
                submission.Outcome,
                submission.FailureCode,
                submission.FailureDetail,
                nowUtc,
                cancellationToken);
            return;
        }

        // Succeeded, Partial and Rejected are all final. Partial counts as
        // done because the case did reach EVA, and Rejected because the same
        // payload will be refused again.
        await RecordAsync(
            workItem,
            submission.Outcome == EvaSubmissionOutcome.Rejected
                ? EvaSubmissionWorkState.Failed
                : EvaSubmissionWorkState.Completed,
            submission.FailureCode,
            submission.FailureDetail,
            dueAtUtc: null,
            nowUtc,
            cancellationToken);
    }

    private async Task RescheduleOrFailAsync(
        EvaSubmissionWorkItem workItem,
        EvaSubmissionOutcome outcome,
        string? failureCode,
        string? failureReason,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var delay = EvaSubmissionRetryPolicy.NextAttemptDelay(workItem.AttemptCount, outcome);
        await RecordAsync(
            workItem,
            delay is null ? EvaSubmissionWorkState.Failed : EvaSubmissionWorkState.RetryScheduled,
            failureCode,
            failureReason,
            delay is null ? null : nowUtc.Add(delay.Value),
            nowUtc,
            cancellationToken);
    }

    private Task RecordAsync(
        EvaSubmissionWorkItem workItem,
        EvaSubmissionWorkState state,
        string? failureCode,
        string? failureReason,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        workStore.RecordOutcomeAsync(
            workItem.Id,
            workItem.LeaseToken!,
            state,
            failureCode,
            failureReason,
            dueAtUtc,
            nowUtc,
            cancellationToken);
}
