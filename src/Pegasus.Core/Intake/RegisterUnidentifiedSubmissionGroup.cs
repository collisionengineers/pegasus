using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

/// <summary>
/// Registers a submission group Unidentified once its members' files are in
/// Box holding: the one owner of that hold, whether the group's routing
/// resolved it Unidentified (<c>ProcessQueuedIntake</c>) or its escape window
/// closed first (<see cref="ReconcileGroupedImageIntake"/>). Nothing is held
/// while the group is pending, so every member stays in retained blob storage
/// until then (operator, 19 October 2026; R382). It holds the member receipts
/// the caller's pass already read and never reads the group again.
/// </summary>
public sealed class RegisterUnidentifiedSubmissionGroup(
    ProcessIntake processIntake,
    IRegisterUnidentified registerUnidentified)
{
    /// <summary>
    /// Holds each of <paramref name="members"/> that holding requires, then
    /// registers <paramref name="request"/>. An unconfirmed hand-over throws
    /// before anything is registered, so the caller's retry runs both again.
    /// </summary>
    public async Task ExecuteAsync(
        RegisterUnidentifiedRequest request,
        IEnumerable<IntakeReceipt> members,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(members);
        foreach (var member in members)
        {
            if (ProcessIntake.RequiresHolding(member, AutomaticCaseEvidencePromotionOutcome.NotApplicable))
            {
                _ = await processIntake.RetainHoldingAssetsAsync(member, cancellationToken);
            }
        }

        await registerUnidentified.ExecuteAsync(request, cancellationToken);
    }
}
