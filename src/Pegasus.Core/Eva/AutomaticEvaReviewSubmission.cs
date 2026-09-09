namespace Pegasus.Core.Eva;

/// <summary>
/// A durable automatic API intention created only by the Review transition.
/// It records an attempted external hand-off; it is never inferred later from
/// a changed Principal setting, so changing a setting cannot send old cases.
/// </summary>
public sealed record AutomaticEvaReviewSubmissionIntent(
    Guid Id,
    Guid CaseId,
    long WorkflowVersion,
    string OperationKey,
    AutomaticEvaReviewSubmissionState State);

public enum AutomaticEvaReviewSubmissionState
{
    Pending,
    Dispatching,
    Completed,
    ReconciliationRequired
}

public sealed record AutomaticEvaReviewSubmissionClaim(
    AutomaticEvaReviewSubmissionIntent Intent,
    string LeaseToken);

public interface IAutomaticEvaReviewSubmissionStore
{
    Task<AutomaticEvaReviewSubmissionClaim?> ClaimAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        Guid intentId,
        string leaseToken,
        EvaSubmissionOutcome outcome,
        CancellationToken cancellationToken);
}
