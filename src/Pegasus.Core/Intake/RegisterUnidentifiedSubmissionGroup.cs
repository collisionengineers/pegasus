using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

/// <summary>
/// Registers a submission group Unidentified once its members' files are in
/// Box holding: the one owner of that hold, whether the group's routing
/// resolved it Unidentified (<c>ProcessQueuedIntake</c>) or its escape window
/// closed first (<see cref="ReconcileGroupedImageIntake"/>). Nothing is held
/// while the group is pending, so every member stays in retained blob storage
/// until then (operator, 19 October 2026; R382). It holds the member receipts
/// the caller's pass already read and never reads the group or a member
/// receipt again. A photo link's group is never Unidentified (FRD-28): it is
/// neither held nor registered here.
/// </summary>
public sealed class RegisterUnidentifiedSubmissionGroup(
    ProcessIntake processIntake,
    IRegisterUnidentified registerUnidentified)
{
    /// <summary>
    /// Holds each of <paramref name="members"/> that holding requires, then
    /// registers <paramref name="request"/>. An unconfirmed hand-over throws
    /// before anything is registered, so the caller's retry runs both again.
    /// Returns whether the group was registered: one from a
    /// <paramref name="channel"/> that never becomes Unidentified is not.
    /// </summary>
    public async Task<bool> ExecuteAsync(
        IntakeSourceChannel channel,
        RegisterUnidentifiedRequest request,
        IEnumerable<IntakeReceipt> members,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(members);
        if (!PhotoCapture.PhotoCaptureIntakeRules.BecomesUnidentified(channel))
        {
            return false;
        }

        foreach (var member in members)
        {
            if (ProcessIntake.RequiresHolding(member, AutomaticCaseEvidencePromotionOutcome.NotApplicable))
            {
                await processIntake.HoldAssetsAsync(member, cancellationToken);
            }
        }

        await registerUnidentified.ExecuteAsync(request, cancellationToken);
        return true;
    }
}
