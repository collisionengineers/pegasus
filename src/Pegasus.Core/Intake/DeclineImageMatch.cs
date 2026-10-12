using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public interface IDeclineImageMatch
{
    Task ExecuteAsync(
        Guid groupId,
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Staff left a manual upload's matched Cases undecided (operator, 11 October
/// 2026). The decline is recorded once on the group, then each member's
/// completed work is re-driven so the images register as an Image-initiated
/// Case now rather than at the end of the wait. The enqueue is best effort;
/// <see cref="ReconcileGroupedImageIntake"/> is the backstop.
/// </summary>
public sealed class DeclineImageMatch(
    IIntakeSubmissionGroupStore groups,
    IIntakeWorkEnqueuer workEnqueuer,
    TimeProvider timeProvider) : IDeclineImageMatch
{
    public async Task ExecuteAsync(
        Guid groupId,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
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
