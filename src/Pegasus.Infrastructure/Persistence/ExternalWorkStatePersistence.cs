using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// Owns the persisted <see cref="ExternalWorkItemEntity.State"/> vocabulary
/// and its mapping to the Core EVA-submission work state.
/// </summary>
internal static class ExternalWorkStatePersistence
{
    public const string Pending = "pending";
    public const string Dispatching = "dispatching";
    public const string Queued = "queued";
    public const string Processing = "processing";
    public const string Completed = "completed";
    public const string Failed = "failed";

    internal static EvaSubmissionWorkState ParseEvaSubmission(
        string value,
        int attemptCount) => value switch
        {
            Pending when attemptCount > 0 => EvaSubmissionWorkState.RetryScheduled,
            Pending or Dispatching or Queued => EvaSubmissionWorkState.Pending,
            Processing => EvaSubmissionWorkState.Processing,
            Completed => EvaSubmissionWorkState.Completed,
            Failed => EvaSubmissionWorkState.Failed,
            _ => throw new InvalidDataException(
                $"The EVA submission work item has unknown state '{value}'.")
        };

    internal static string FormatEvaSubmission(EvaSubmissionWorkState state) => state switch
    {
        EvaSubmissionWorkState.RetryScheduled => Pending,
        EvaSubmissionWorkState.Completed => Completed,
        EvaSubmissionWorkState.Failed => Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}
