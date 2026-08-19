using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// The reverse half of automatic pairing: images commonly arrive before the
/// instruction, so when a Case is accepted this hook checks every Image
/// intake still Awaiting instruction against the new case and associates each
/// one whose registered identity exactly equals the new case's confirmed
/// registration and for which that case is the single eligible candidate.
/// Exact only: the near-miss completions apply at scan time, where the
/// confirmed registration becomes the registered identity — an Image
/// intake's identity is already immutable here, so a near-miss cannot be
/// completed and stays a reasoned staff suggestion. Advisory and
/// non-blocking: acceptance stands regardless of any pairing failure, and
/// each intake's automatic association still runs at most once (its
/// operation key is origin-receipt-scoped across both pairing directions).
/// </summary>
/// <remarks>
/// <see cref="SyncMergeAfterLinkAsync"/> is the one owner of "an origin
/// receipt just gained a Case association; bring its Image-initiated Case
/// lifecycle into line" — the automatic forward path
/// (<c>ImageIntakeAutomation</c>), this reverse path, and the manual staff
/// <c>LinkIntake</c> path all call it instead of each carrying its own copy
/// of the merge transition. Because the merge operation key is deterministic
/// per origin receipt and the transition is replay-safe, a merge that fails
/// after its association already committed is not lost: the next call from
/// any of those three entry points — the next accepted case, the next manual
/// link action, or a later automatic pass — retries the same idempotent
/// transition rather than leaving the record permanently stuck.
/// </remarks>
public interface IImageIntakeCasePairing
{
    Task PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken);

    /// <summary>
    /// Brings the Image-initiated Case lifecycle for <paramref name="originReceiptId"/>
    /// into line with a Case association that was just made (or already
    /// exists). A no-op when there is no registered Image intake for that
    /// receipt, or it is not (or no longer) Awaiting instruction — so it is
    /// always safe to call after any successful link, and safe to call again
    /// to retry a merge that did not commit last time.
    /// </summary>
    Task SyncMergeAfterLinkAsync(
        Guid originReceiptId,
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken);
}

public sealed class ImageIntakeCasePairing(
    IImageIntakeStore imageIntakeStore,
    IImageIntakeCaseCandidates caseCandidates,
    IIntakeMutationStore intakeMutationStore,
    TimeProvider timeProvider) : IImageIntakeCasePairing
{
    public async Task PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return;
        }

        // The candidate set is every Image intake still Awaiting instruction
        // — not "has no Case association" (item 8: a Staff-closed or already
        // Merged record can carry no association yet must never be treated as
        // a pairing candidate again), and it deliberately includes a record
        // that is already associated with this case but whose merge did not
        // yet commit, so a prior partial failure gets retried here.
        var all = await imageIntakeStore.ListAsync(associated: null, cancellationToken);
        var awaiting = all.Where(intake => intake.State == ImageInitiatedCaseState.AwaitingInstruction);
        var actor = ActionActor.SystemWorker(ImageIntakeAutomation.ActorId);
        foreach (var intake in awaiting)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (intake.AssociatedCaseId == caseId)
                {
                    await SyncMergeAfterLinkAsync(intake.OriginReceiptId, caseId, actor, cancellationToken);
                    continue;
                }

                if (intake.AssociatedCaseId is not null)
                {
                    continue;
                }

                var eligible = await caseCandidates.FindEligibleByRegistrationAsync(
                    intake.NormalizedVehicleRegistration,
                    cancellationToken);
                if (eligible.Count != 1
                    || eligible[0].CaseId != caseId
                    || !string.Equals(
                        eligible[0].ConfirmedRegistration,
                        intake.NormalizedVehicleRegistration,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                await intakeMutationStore.AutoLinkAsync(
                    new(
                        intake.OriginReceiptId,
                        caseId,
                        eligible[0].CaseVersion,
                        actor,
                        $"image-intake-associate:{intake.OriginReceiptId:N}",
                        $"Automatic association: the newly accepted case {eligible[0].CaseReference} matches this Image intake's confirmed registration unambiguously."),
                    timeProvider.GetUtcNow(),
                    cancellationToken);
                await SyncMergeAfterLinkAsync(intake.OriginReceiptId, caseId, actor, cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // One intake failing to pair (version race, staff lease, an
                // already-consumed automatic association) never affects the
                // others or the acceptance itself; it is retried on the next
                // qualifying pass through this method or through the manual
                // link path.
            }
        }
    }

    public async Task SyncMergeAfterLinkAsync(
        Guid originReceiptId,
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (originReceiptId == Guid.Empty || caseId == Guid.Empty)
        {
            return;
        }

        var detail = await imageIntakeStore.GetByOriginReceiptAsync(originReceiptId, cancellationToken);
        if (detail is null || detail.State != ImageInitiatedCaseState.AwaitingInstruction)
        {
            return;
        }

        await imageIntakeStore.MergeAsync(
            new(
                detail.Record.Id,
                caseId,
                actor,
                $"image-intake-merge:{originReceiptId:N}",
                $"The Image-initiated case {detail.Record.ImageIntakeReference} was merged into the linked formal Case.",
                detail.LifecycleVersion),
            cancellationToken);
    }
}
