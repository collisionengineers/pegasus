using Pegasus.Core.ImageIntake;

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
/// own reconciliation mechanism for that race: a member is never recovered by
/// manual SQL, only by re-driving its already-completed durable work item
/// through the ordinary pipeline (<see cref="IProcessQueuedIntake"/> →
/// <see cref="IImageIntakeAutomation"/>), which is safe to call repeatedly
/// because that work item's evaluation is already complete — the call takes
/// the cheap replay branch, never re-reading the (already deleted) staged
/// artifact. Only image-group members are re-driven here; a receipt
/// associated with a Case is filed by <see cref="ReplayCaseFilings"/>.
/// </summary>
/// <remarks>
/// A receipt still pending once its group's <see cref="IntakeEscapeWindow"/>
/// has closed is registered Unidentified here instead, every member held
/// first — the bounded-retry-with-poison-escape shape used elsewhere in
/// intake processing, measured in age because this receipt's own work item
/// never re-attempts on its own; only this sweep, and the ordinary pipeline's
/// first pass, ever touch it.
/// <para>
/// A manual upload whose images matched existing Cases waits for staff with
/// nothing registered (operator, 11 October 2026). This sweep is also what
/// re-drives it once the window closes: the replay then registers the
/// Image-initiated Case, and that wait is never escaped to Unidentified.
/// </para>
/// </remarks>
public sealed class ReconcileGroupedImageIntake(
    IIntakeReceiptQueries receiptQueries,
    IIntakeSubmissionGroupStore groupStore,
    IIntakeWorkStore workStore,
    IProcessQueuedIntake processQueuedIntake,
    RegisterUnidentifiedSubmissionGroup unidentifiedGroups)
{
    public async Task<ReconcileGroupedImageIntakeResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);

        var receiptIds = await groupStore.ListPendingImageGroupReceiptsAsync(
            maximumItems,
            cancellationToken);

        var candidates = 0;
        var retried = 0;
        var escaped = 0;
        var failures = 0;
        foreach (var receiptId in receiptIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receipt = await receiptQueries.GetAsync(receiptId, cancellationToken);
            if (receipt is null
                || receipt.Decision != IntakeDecision.NeedsSorting
                || !ImageIntakeLifecycleRules.IsImageAutomationEligible(receipt))
            {
                continue;
            }

            var group = await groupStore.FindForMemberSourceAsync(
                receipt.SourceIdentity,
                cancellationToken);
            if (group is null)
            {
                continue;
            }

            candidates++;
            try
            {
                var stagedReceiptId = await workStore.FindStagedReceiptIdForReceiptAsync(
                    receipt.Id,
                    cancellationToken);
                if (stagedReceiptId is null)
                {
                    failures++;
                    continue;
                }

                var outcome = await processQueuedIntake.ExecuteAsync(stagedReceiptId.Value, cancellationToken);
                retried++;
                if (outcome == QueuedIntakeProcessingOutcome.RetryScheduled
                    && IntakeEscapeWindow.HasClosed(group)
                    && await receiptQueries.GetAsync(receipt.Id, cancellationToken) is
                        { Decision: IntakeDecision.NeedsSorting } current)
                {
                    await unidentifiedGroups.ExecuteAsync(
                        ImageIntakeGroupRoutingPolicy.BuildUnidentifiedRegistrationRequest(group, "group_processing_incomplete"),
                        await ReadMembersAsync(group, current, cancellationToken),
                        cancellationToken);
                    escaped++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
            }
        }

        return new(candidates, retried, escaped, failures);
    }

    /// <summary>
    /// The escaping group's member receipts, each read once: the receipt this
    /// pass just re-read stands for itself, and a member with no receipt yet
    /// has nothing to hold.
    /// </summary>
    private async Task<IReadOnlyList<IntakeReceipt>> ReadMembersAsync(
        IntakeSubmissionGroup group,
        IntakeReceipt current,
        CancellationToken cancellationToken)
    {
        var members = new List<IntakeReceipt>(group.Members.Count);
        foreach (var member in group.Members.OrderBy(item => item.Ordinal))
        {
            var source = new IntakeSourceIdentity(
                group.Channel,
                GroupedIntakeMemberToken.Create(group.SubmissionToken, member.Ordinal));
            if (source == current.SourceIdentity)
            {
                members.Add(current);
            }
            else if (await receiptQueries.FindBySourceIdentityAsync(source, cancellationToken) is { } receipt)
            {
                members.Add(receipt);
            }
        }

        return members;
    }
}
