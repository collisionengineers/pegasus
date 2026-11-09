using System.Globalization;
using Pegasus.Core.Intake;

namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// What the intake pipeline does differently for a photo link's receipts
/// (FRD-28, FRD-19). Named here, once, so the pipeline asks instead of
/// re-deriving it from the channel in several places.
/// </summary>
public static class PhotoCaptureIntakeRules
{
    public const string SettleActorId = "photo-capture-settle";
    public const string RoutingActorId = "photo-capture-routing";

    /// <summary>The submission token of one capture's batch; its members follow <see cref="GroupedIntakeMemberToken"/>.</summary>
    public static string BatchToken(Guid captureId, int batch) =>
        string.Create(CultureInfo.InvariantCulture, $"{captureId:N}-{batch}");

    public static string MemberToken(Guid captureId, int batch, int ordinal) =>
        GroupedIntakeMemberToken.Create(BatchToken(captureId, batch), ordinal);

    /// <summary>The deterministic group id of one batch, so a settle that is retried creates no second group.</summary>
    public static Guid BatchGroupId(Guid captureId, int batch)
    {
        var bytes = captureId.ToByteArray();
        bytes[15] = unchecked((byte)(bytes[15] ^ batch));
        bytes[14] = unchecked((byte)(bytes[14] ^ (batch >> 8)));
        return new Guid(bytes);
    }

    /// <summary>
    /// A photo link's photos complete a Case's images when the capture
    /// settles, not when the first one files (operator, 9 November 2026), so
    /// a Case is never moved on while the customer is still taking pictures.
    /// </summary>
    public static bool DefersCaseImageCompletion(IntakeSourceChannel channel) =>
        channel == IntakeSourceChannel.PhotoCapture;

    /// <summary>
    /// A capture's receipt is never Unidentified: a file the capture surface
    /// could not take was refused at the door, and a receipt that waits does
    /// so for its capture to settle, not for a person to sort it.
    /// </summary>
    public static bool BecomesUnidentified(IntakeSourceChannel channel) =>
        channel != IntakeSourceChannel.PhotoCapture;

    /// <summary>
    /// A capture's receipt is never held in the Box holding folder: its
    /// destination is settled by its capture, on the Case as it arrives or in
    /// the Vehicle images record its batch becomes (FRD-05: holding is for
    /// intake with no settled destination).
    /// </summary>
    public static bool IsHeld(IntakeSourceChannel channel) =>
        channel != IntakeSourceChannel.PhotoCapture;

    /// <summary>
    /// A capture's receipt with no group yet is waiting for its capture to
    /// settle; image automation leaves it exactly as the pipeline decided it,
    /// registers nothing and holds nothing, and the settle forms the group
    /// that routes it.
    /// </summary>
    public static bool WaitsForSettle(IntakeReceipt receipt, IntakeSubmissionGroup? group)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return receipt.SourceIdentity.Channel == IntakeSourceChannel.PhotoCapture && group is null;
    }

    /// <summary>
    /// A capture's group carries the registration its sender typed, which
    /// stands in for plate recognition in the group's routing (operator,
    /// 9 November 2026): every image member reads as that registration.
    /// </summary>
    public static bool UsesDeclaredRegistration(IntakeSubmissionGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group.Channel == IntakeSourceChannel.PhotoCapture && group.DeclaredRegistration is not null;
    }
}
