using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PrincipalApi;

/// <summary>
/// <paramref name="FirstFailure"/> is the type and message of the first
/// swallowed failure of the pass, or null when there was none. A count on its
/// own cannot tell a missing grant from a dropped connection, and this sweep
/// runs every ten seconds: without the cause, a deployment that skipped a
/// migration reads as a steady stream of healthy-looking zeros.
/// </summary>
public sealed record ReconcilePrincipalSubmissionsResult(
    int Candidates,
    int Repaired,
    int Failures,
    string? FirstFailure);

/// <summary>
/// The one owner of the Principal API accept-recovery rule: a submission whose
/// durable intake receipt exists but whose initial Accepted history write was
/// interrupted is completed by the existing intake reconciliation timer. A bare submission reservation is not a candidate at
/// all — the store excludes it — because a retry still owns its intake
/// attempt and no sweep can ever complete it.
/// </summary>
public sealed class ReconcilePrincipalSubmissions(
    IPrincipalSubmissionStore submissionStore,
    IActionHistoryWriter actionHistory,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Lets the inline request finish its separate writes before the sweep
    /// touches the same submission. It is not what keeps the acceptance
    /// single — the derived history identity
    /// (<see cref="PrincipalSubmissionPolicy.AcceptedHistoryId"/>) is, and it
    /// holds however long a request runs — it just leaves a request that is
    /// still in flight to record its own correlation id.
    /// </summary>
    public static readonly TimeSpan AcceptHistoryGracePeriod = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Says in permanent history that this row was completed by recovery, and
    /// why it carries an operation key where an accept written inline carries
    /// the request's correlation id.
    /// </summary>
    private const string RecoveredAcceptReason =
        "Completed by accept recovery; this row carries the submission's own operation key rather than a request correlation id.";

    public async Task<ReconcilePrincipalSubmissionsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var candidates = await submissionStore.ListAcceptRecoveryCandidatesAsync(
            maximumItems,
            cancellationToken);
        var repaired = 0;
        var failures = 0;
        string? firstFailure = null;
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
                // Stamped with when the submission was received, not with this
                // sweep's clock: the acceptance happened then, and a row
                // claiming now would be ordered after the Replayed row it
                // precedes. The originating request's correlation id went with
                // the process that never wrote this row, so the row carries the
                // submission's own operation key and says where it came from
                // instead of presenting a substitute as a request id.
                //
                // False means the inline request appended its own row between
                // the candidate read and this write: that row is the
                // acceptance, this pass repaired nothing, and neither fact is
                // hidden.
                if (await actionHistory.TryAppendAsync(
                        new(
                            PrincipalSubmissionPolicy.AcceptedHistoryId(candidate.SubmissionId),
                            PrincipalSubmissionPolicy.ActionHistoryAggregateType,
                            candidate.SubmissionId.ToString("D"),
                            "Submitted",
                            ActionActor.Principal(candidate.PrincipalId),
                            candidate.ReceivedAtUtc.ToUniversalTime(),
                            "Accepted",
                            PrincipalSubmissionPolicy.OperationKey(candidate.SubmissionId),
                            RecoveredAcceptReason),
                        cancellationToken))
                {
                    repaired++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
                firstFailure ??= $"{exception.GetType().Name}: {exception.Message}";
            }
        }

        return new(candidates.Count, repaired, failures, firstFailure);
    }
}
