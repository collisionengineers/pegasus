using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

public sealed record ReconcileGroupedImageIntakeResult(
    int Candidates,
    int Retried,
    int Escaped,
    int Failures);

/// <summary>
/// Recovers a grouped-image straggler: a receipt still at
/// <see cref="IntakeDecision.NeedsSorting"/> whose group may since have
/// resolved (a sibling already registered), or whose own registration
/// attempt lost a transient concurrency race and was deferred by
/// <c>ProcessQueuedIntake</c> without a U-reference. This is the product's
/// own reconciliation mechanism for INTK-011: a member is never recovered by
/// manual SQL, only by re-driving its already-completed durable work item
/// through the ordinary pipeline (<see cref="IProcessQueuedIntake"/> →
/// <see cref="IImageIntakeAutomation"/>), which is safe to call repeatedly
/// because that work item's evaluation is already complete — the call takes
/// the cheap replay branch, never re-reading the (already deleted) staged
/// artifact.
/// </summary>
/// <remarks>
/// A receipt still pending after <see cref="EscapeAfter"/> is registered
/// Unidentified here instead — the bounded-retry-with-poison-escape shape
/// used elsewhere in intake processing, measured in wall-clock age because
/// this receipt's own work item never re-attempts on its own; only this
/// sweep, and the ordinary pipeline's first pass, ever touch it.
/// </remarks>
public sealed class ReconcileGroupedImageIntake(
    IIntakeReceiptQueries receiptQueries,
    IIntakeSubmissionGroupStore groupStore,
    IIntakeWorkStore workStore,
    IProcessQueuedIntake processQueuedIntake,
    TimeProvider timeProvider,
    IRegisterUnidentified registerUnidentified)
{
    /// <summary>
    /// How long a grouped-image receipt may sit pending before this sweep
    /// gives up waiting for its group and registers it Unidentified instead.
    /// Matches the longest delay in a genuine processing failure's retry
    /// budget (<c>ProcessQueuedIntake.RetryDelays</c>) rather than inventing a
    /// second bound.
    /// </summary>
    public static readonly TimeSpan EscapeAfter = TimeSpan.FromHours(2);

    public async Task<ReconcileGroupedImageIntakeResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var page = await receiptQueries.ListAsync(
            IntakeDecision.NeedsSorting,
            page: 1,
            pageSize: maximumItems,
            cancellationToken);

        var candidates = 0;
        var retried = 0;
        var escaped = 0;
        var failures = 0;
        var nowUtc = timeProvider.GetUtcNow();
        foreach (var summary in page.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = await receiptQueries.GetAsync(summary.Id, cancellationToken);
            if (receipt is null
                || receipt.Decision != IntakeDecision.NeedsSorting
                || !ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt))
            {
                continue;
            }

            var group = await groupStore.FindForMemberSourceAsync(
                receipt.SourceIdentity,
                cancellationToken);
            if (group is null || !group.HasSiblingMembers)
            {
                // Not a multi-member upload: no sibling can ever change this
                // receipt's outcome, so it is not this reconciliation's
                // concern (the ordinary single-receipt automation already had
                // its chance).
                continue;
            }

            candidates++;
            try
            {
                if (nowUtc - receipt.ProcessedAtUtc >= EscapeAfter)
                {
                    await registerUnidentified.ExecuteAsync(
                        ProcessIntake.BuildUnidentifiedRegistrationRequest(receipt),
                        cancellationToken);
                    escaped++;
                    continue;
                }

                var stagedReceiptId = await workStore.FindStagedReceiptIdForReceiptAsync(
                    receipt.Id,
                    cancellationToken);
                if (stagedReceiptId is null)
                {
                    failures++;
                    continue;
                }

                await processQueuedIntake.ExecuteAsync(stagedReceiptId.Value, cancellationToken);
                retried++;
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(candidates, retried, escaped, failures);
    }
}
