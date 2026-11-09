using System.Globalization;
using Pegasus.Core.Identity;
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

    /// <summary>The Intake log's reason for a photo filed on the Case its link was sent from (operator, 9 November 2026; R805).</summary>
    public const string CaseLinkDestinationReason = "Destination declared by the photo link sent from this Case.";

    /// <summary>The Intake log's reason for a photo filed on the one Case its typed registration routed to (R805).</summary>
    public const string RoutedDestinationReason = "Destination chosen from the registration typed on the photo link.";

    /// <summary>The Intake log's reason for a photo that waits for its capture to be sent (R805).</summary>
    public const string WaitingReason = "Photo link item: waiting for its capture to be sent; no identification ran.";

    /// <summary>The submission token of one capture's batch; its members follow <see cref="GroupedIntakeMemberToken"/>.</summary>
    public static string BatchToken(Guid captureId, int batch) =>
        string.Create(CultureInfo.InvariantCulture, $"{captureId:N}-{batch}");

    public static string MemberToken(Guid captureId, int batch, int ordinal) =>
        GroupedIntakeMemberToken.Create(BatchToken(captureId, batch), ordinal);

    /// <summary>
    /// The inverse of <see cref="MemberToken"/>: the capture, batch and
    /// ordinal an item's receipt token names, so the item can be found by its
    /// receipt before it has recorded which staged receipt it became.
    /// </summary>
    public static bool TryReadMemberToken(string? memberToken, out Guid captureId, out int batch, out int ordinal)
    {
        captureId = Guid.Empty;
        batch = 0;
        ordinal = 0;
        if (string.IsNullOrWhiteSpace(memberToken))
        {
            return false;
        }

        var batchToken = memberToken;
        var separator = memberToken.LastIndexOf(':');
        if (separator >= 0)
        {
            // Create writes no ":0": the first member carries the batch token alone.
            if (!int.TryParse(memberToken.AsSpan(separator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out ordinal)
                || ordinal < 1)
            {
                ordinal = 0;
                return false;
            }

            batchToken = memberToken[..separator];
        }

        var dash = batchToken.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0
            || !Guid.TryParseExact(batchToken[..dash], "N", out captureId)
            || !int.TryParse(batchToken.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out batch)
            || batch < 1)
        {
            captureId = Guid.Empty;
            batch = 0;
            ordinal = 0;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Who decided where a capture's photos file (operator, 9 November 2026;
    /// R790): a link sent from a Case is the decision of the member of staff
    /// who sent it, so its photos file on any Case that is not archived; a
    /// capture Pegasus routed to its one Case is the routing's system work.
    /// A capture that could not be read is taken as routed.
    /// </summary>
    public static ActionActor DeclaredDestinationDecider(PhotoCapture? capture) =>
        IssuingStaffMember(capture) is { } staffId
            ? ActionActor.Staff(staffId, [StaffRole.User])
            : ActionActor.SystemWorker(RoutingActorId);

    /// <summary>The Intake log's reason for <see cref="DeclaredDestinationDecider"/>'s decision: one per decider (R805).</summary>
    public static string DeclaredDestinationReason(PhotoCapture? capture) =>
        IssuingStaffMember(capture) is null ? RoutedDestinationReason : CaseLinkDestinationReason;

    private static Guid? IssuingStaffMember(PhotoCapture? capture) =>
        capture is { Origin: PhotoCaptureOrigin.CaseLink, IssuedByStaffId: { } staffId } && staffId != Guid.Empty
            ? staffId
            : null;

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
    /// A capture's receipt routes with its batch's group whatever it holds,
    /// a video or paperwork included: the group reads as the registration
    /// the sender typed, so no member needs to be an image to drive it. The
    /// grouped-image sweep re-drives a capture's group through any member,
    /// as its listing (<c>ListPendingImageGroupReceiptsAsync</c>) admits it.
    /// </summary>
    public static bool RoutesWithItsGroup(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return receipt.SourceIdentity.Channel == IntakeSourceChannel.PhotoCapture;
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
