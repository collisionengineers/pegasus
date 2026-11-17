using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Intake;

/// <summary>
/// One file of an upload as Leave reads it: whether its outcome is a possible
/// match, which the upload page works out, and its receipt.
/// </summary>
public sealed record ImageMatchLeaveFile(bool PossibleMatch, IntakeReceipt? Receipt);

/// <summary>
/// Leave, declining the image match so the images register, is offered only
/// while the upload awaits its decision and every file is a possible match
/// and image-only. The upload pages, their handlers and the Automation Actor's
/// leave all follow this one rule (R1092).
/// </summary>
public static class ImageMatchLeave
{
    public static bool IsOffered(bool awaitsDecision, IReadOnlyCollection<ImageMatchLeaveFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return awaitsDecision
            && files.Count > 0
            && files.All(file => file.PossibleMatch
                && file.Receipt is not null
                && ImageIntakeLifecycleRules.IsImageOnlyMaterial(file.Receipt));
    }
}

public interface IDeclineImageMatch
{
    /// <param name="awaitsDecision">Whether the upload awaits its decision, as the caller reads it.</param>
    /// <param name="files">Each file of the upload, as the caller reads it.</param>
    Task ExecuteAsync(
        Guid groupId,
        bool awaitsDecision,
        IReadOnlyCollection<ImageMatchLeaveFile> files,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Staff left a manual upload's matched Cases undecided (operator, 11 October
/// 2026). The decline is recorded once on the group, then each member's
/// completed work is re-driven so the images register as an Image-initiated
/// Case now rather than at the end of the wait. The enqueue is best effort;
/// <see cref="ReconcileGroupedImageIntake"/> is the backstop. A missing,
/// discarded or non-manual upload is left alone, an upload already left is
/// answered as done (its members re-driven, the decline as first recorded;
/// R1110), and any other is refused where
/// <see cref="ImageMatchLeave.IsOffered"/> does not offer Leave (R1092).
/// </summary>
public sealed class DeclineImageMatch(
    IIntakeSubmissionGroupStore groups,
    IIntakeWorkEnqueuer workEnqueuer,
    TimeProvider timeProvider) : IDeclineImageMatch
{
    public async Task ExecuteAsync(
        Guid groupId,
        bool awaitsDecision,
        IReadOnlyCollection<ImageMatchLeaveFile> files,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(actor);
        if (groupId == Guid.Empty)
        {
            throw new ArgumentException("A submission group identifier is required.", nameof(groupId));
        }
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        var group = await groups.GetAsync(groupId, cancellationToken);
        if (group is null
            || group.Discard is not null
            || group.Channel != IntakeSourceChannel.ManualUpload)
        {
            return;
        }

        // An upload already left is answered as done: the decline stays as
        // first recorded and its members are re-driven, as before (R1110).
        if (group.ImageMatchDeclined is null && !ImageMatchLeave.IsOffered(awaitsDecision, files))
        {
            throw new InvalidOperationException("Leave is not offered on this upload.");
        }

        await groups.DeclineImageMatchAsync(
            group.Id,
            actor.SubjectId,
            timeProvider.GetUtcNow(),
            cancellationToken);
        foreach (var member in group.Members)
        {
            try
            {
                await workEnqueuer.EnqueueAsync(member.StagedReceiptId, cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // The decline stands; the grouped-image sweep re-drives the member.
            }
        }
    }
}
