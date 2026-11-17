namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// The acts a link offers now, which its page shows, its own handlers take
/// and the Automation Actor's photo link tools follow alike (#2130; R1092):
/// Resend on a live link HQ sent from a Case (R914); Log call on one HQ sent
/// from a Case that is not cancelled; Extend on one not cancelled whose Case
/// is not archived (R793); Cancel while it is live.
/// </summary>
public sealed record PhotoLinkOffers(bool Resend, bool LogCall, bool Extend, bool Cancel)
{
    public static PhotoLinkOffers For(PhotoCaptureListRow row, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(row);
        var capture = row.Capture;
        var caseLink = capture.Origin == PhotoCaptureOrigin.CaseLink;
        var live = PhotoCaptureAccess.IsLive(capture, now, row.CaseArchived);
        return new(
            Resend: caseLink && live,
            LogCall: caseLink && !capture.IsRevoked,
            Extend: !capture.IsRevoked && !row.CaseArchived,
            Cancel: live);
    }
}
