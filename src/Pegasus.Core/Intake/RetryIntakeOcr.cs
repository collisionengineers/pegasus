using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>
/// Retry OCR (Administration › Logs and Operations, 13 September): a person asks
/// for a received item's failed OCR to run again, with a reason. The request is
/// idempotent on its operation key and recorded in the intake mutation history.
/// </summary>
public sealed record RetryIntakeOcrRequest(
    Guid ReceiptId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public interface IRetryIntakeOcr
{
    Task<IntakeReceipt> ExecuteAsync(
        RetryIntakeOcrRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>The one owner of when OCR may be retried by a person.</summary>
public static class IntakeOcrRetryPolicy
{
    /// <summary>
    /// Only a last attempt that definitely failed. An operation that is still
    /// pending or scheduled is already going to run; an Unknown one may have
    /// reached the provider and is reconciled, never resent; a completed one has
    /// nothing to retry.
    /// </summary>
    public static bool CanRetry(IntakeOcrState? lastAttemptState) =>
        lastAttemptState == IntakeOcrState.Failed;
}

/// <summary>
/// Web records the retry and puts only the OCR operation's durable external-work
/// row back to pending. Web never changes the operation: it has no UPDATE on it
/// and cannot process OCR. The Worker's external-work dispatch and
/// <see cref="ProcessIntakeOcr"/> then run it: a Failed operation whose work was
/// re-queued is resumed through
/// <see cref="IIntakeOcrOperationStore.ResumeRequestedRetryAsync"/>. A failure
/// whose provider output was already retained is re-analysed from that output;
/// one with none is submitted again.
/// </summary>
public sealed class RetryIntakeOcr(
    IIntakeMutationStore store,
    TimeProvider timeProvider) : IRetryIntakeOcr
{
    public Task<IntakeReceipt> ExecuteAsync(
        RetryIntakeOcrRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        IntakeCommandValidation.RequireStaffMutation(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason);
        return store.ScheduleOcrRetryAsync(request, timeProvider.GetUtcNow(), cancellationToken);
    }
}
