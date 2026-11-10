using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Intake;

public sealed record AssociateThreadSiblingsResult(int Candidates, int Associated, int Failures);

/// <summary>
/// The store <see cref="AssociateThreadSiblings"/> reads its page from.
/// </summary>
public interface IThreadSiblingAssociationCandidates
{
    /// <summary>
    /// Up to <paramref name="maximumItems"/> mailbox receipts, oldest
    /// completion first, that finished processing after
    /// <paramref name="completedAfterUtc"/> standing linked to no Case: no
    /// association row of any kind (a staff unlink is never re-linked), no
    /// accepted link, no Case their allocation made and no Triage they
    /// opened, not decided a Pre-Pegasus case, not registered as image intake
    /// or Unidentified, and not a discarded group's member. Each has another
    /// message in its exact mailbox conversation that now stands linked to a
    /// Case or opened a Triage.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListAsync(
        DateTimeOffset completedAfterUtc,
        int maximumItems,
        CancellationToken cancellationToken);
}

/// <summary>
/// Runs mail association again for a No Case Found e-mail whose own mailbox
/// conversation has since come to hold a Case. Two messages of one
/// conversation are processed in parallel. One may read the thread before the
/// other's Case commits, so it finds no Case and is never looked at again
/// (#1831). This runs within the two hours after the e-mail's own processing
/// (<see cref="IntakeEscapeWindow"/>; operator, 10 November 2026, R884). The
/// association is the e-mail's own: <see cref="AssociateRetainedMailWithCase"/>
/// decides it on all four signals, under the same operation key, so a pass
/// that links nothing changes nothing. <see cref="ReplayCaseFilings"/> files
/// what it links.
/// </summary>
public sealed class AssociateThreadSiblings(
    IThreadSiblingAssociationCandidates candidates,
    IIntakeReceiptQueries receiptQueries,
    AssociateRetainedMailWithCase association,
    TimeProvider timeProvider,
    IImageIntakeAutomation? imageIntakeAutomation = null)
{
    public async Task<AssociateThreadSiblingsResult> ExecuteAsync(
        int maximumItems,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var due = await candidates.ListAsync(
            IntakeEscapeWindow.LatestClosedStart(timeProvider.GetUtcNow()),
            maximumItems,
            cancellationToken);
        var associated = 0;
        var failures = 0;
        foreach (var receiptId in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var receipt = await receiptQueries.GetAsync(receiptId, cancellationToken);
                if (receipt is null)
                {
                    continue;
                }

                var outcome = await association.ExecuteForReceiptAsync(
                    receipt, imageIntakeAutomation, cancellationToken);
                if (outcome == AutomaticCaseAssociationOutcome.Associated)
                {
                    associated++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // The evidence changed under the write, or a store fault: the
                // next pass inside the window reads it again.
                failures++;
            }
        }

        return new(due.Count, associated, failures);
    }
}
