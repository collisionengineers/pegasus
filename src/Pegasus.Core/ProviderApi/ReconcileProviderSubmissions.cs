using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ProviderApi;

public sealed record ReconcileProviderSubmissionsResult(
    int Candidates,
    int Repaired,
    int Failures);

/// <summary>
/// The one owner of the Provider API accept-recovery rule: a submission whose
/// durable intake receipt exists but whose provider row or initial Accepted
/// history write was interrupted is completed by the existing intake
/// reconciliation timer. A bare submission reservation is not a candidate at
/// all — the store excludes it — because a retry still owns its intake
/// attempt and no sweep can ever complete it.
/// </summary>
public sealed class ReconcileProviderSubmissions(
    IProviderSubmissionStore submissionStore,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Lets the inline request finish its separate writes before the sweep
    /// considers the missing history row recoverable.
    /// </summary>
    public static readonly TimeSpan AcceptHistoryGracePeriod = TimeSpan.FromMinutes(1);

    public async Task<ReconcileProviderSubmissionsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var candidates = await submissionStore.ListAcceptRecoveryCandidatesAsync(
            maximumItems,
            cancellationToken);
        var repaired = 0;
        var failures = 0;
        var nowUtc = timeProvider.GetUtcNow();
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (nowUtc - candidate.ReceivedAtUtc < AcceptHistoryGracePeriod)
            {
                continue;
            }

            try
            {
                var wasRepaired = false;
                if (candidate.StagedReceiptId is null)
                {
                    await submissionStore.RecordStagedReceiptAsync(
                        candidate.SubmissionId,
                        candidate.RetainedStagedReceiptId,
                        cancellationToken);
                    wasRepaired = true;
                }

                if (!candidate.HasAcceptedHistory)
                {
                    // The Functions timer is singleton, so sweep-versus-sweep
                    // concurrency is not the risk here; the inline request is.
                    // Re-read immediately before appending the missing first
                    // history row so a request that finished during this pass
                    // does not receive a second Accepted row.
                    var current = await submissionStore.GetAcceptRecoveryCandidateAsync(
                        candidate.SubmissionId,
                        cancellationToken)
                        ?? throw new InvalidOperationException(
                            $"Provider submission '{candidate.SubmissionId:D}' disappeared during accept recovery.");
                    if (!current.HasAcceptedHistory)
                    {
                        await actionHistory.AppendAsync(
                            new(
                                Guid.NewGuid(),
                                ProviderSubmissionPolicy.ActionHistoryAggregateType,
                                candidate.SubmissionId.ToString("D"),
                                "Submitted",
                                ActionActor.Provider(candidate.PrincipalId),
                                timeProvider.GetUtcNow(),
                                "Accepted",
                                ProviderSubmissionPolicy.OperationKey(candidate.SubmissionId)),
                            cancellationToken);
                        wasRepaired = true;
                    }
                }

                if (wasRepaired)
                {
                    repaired++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(candidates.Count, repaired, failures);
    }
}
