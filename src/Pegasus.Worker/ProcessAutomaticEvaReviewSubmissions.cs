using Microsoft.Extensions.Logging;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;

namespace Pegasus.Worker;

/// <summary>
/// Executes only intentions that were durably recorded with a Review
/// transition. Unknown delivery is completed as reconciliation-required, so
/// a queue replay never becomes an unbounded API retry.
/// </summary>
public sealed partial class ProcessAutomaticEvaReviewSubmissions(
    IAutomaticEvaReviewSubmissionStore intents,
    ISubmitCaseToEva submitCaseToEva,
    TimeProvider timeProvider,
    ILogger<ProcessAutomaticEvaReviewSubmissions> logger)
{
    private static readonly ActionActor Worker =
        ActionActor.SystemWorker("automatic-eva-review-submission");

    public async Task<int> ExecuteAsync(int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var processed = 0;
        while (processed < maximumItems)
        {
            var claim = await intents.ClaimAsync(
                timeProvider.GetUtcNow(), TimeSpan.FromMinutes(10), cancellationToken);
            if (claim is null)
            {
                return processed;
            }

            try
            {
                var result = await submitCaseToEva.ExecuteAsync(new(
                    claim.Intent.CaseId,
                    Worker,
                    claim.Intent.OperationKey,
                    EvaSubmissionInitiator.AutomaticReview), cancellationToken);
                // A local refusal has no retained EVA outcome for the case.
                // Keep the intent terminal and make a staff retry available,
                // rather than allowing the timer to retry automatically.
                await intents.CompleteAsync(
                    claim.Intent.Id,
                    claim.LeaseToken,
                    result?.Submission?.Outcome ?? EvaSubmissionOutcome.Unknown,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A transport failure, in-progress operation or lost local
                // completion leaves EVA's outcome uncertain. Retain the
                // intent for reconciliation and never let the timer resend.
                LogReconciliationRequired(logger, claim.Intent.CaseId);
                await intents.CompleteAsync(
                    claim.Intent.Id,
                    claim.LeaseToken,
                    EvaSubmissionOutcome.Unknown,
                    cancellationToken);
            }
            processed++;
        }
        return processed;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Automatic EVA Review submission for Case {CaseId} requires reconciliation.")]
    private static partial void LogReconciliationRequired(ILogger logger, Guid caseId);
}
