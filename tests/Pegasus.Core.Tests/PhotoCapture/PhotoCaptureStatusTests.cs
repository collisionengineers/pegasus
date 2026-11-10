using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureStatusTests
{
    private static readonly MissingCaptureItem[] Nothing = [];
    private static readonly MissingCaptureItem[] Plate = [new("plate", "No number plate photo")];

    [Fact]
    public void AFreshLinkIsSentNotOpenedThenOpenedThenPhotosComingIn()
    {
        Assert.Equal(PhotoCaptureState.SentNotOpened, PhotoCaptureStatus.Evaluate(Capture(), Plate, 0, Now));
        Assert.Equal(PhotoCaptureState.Opened, PhotoCaptureStatus.Evaluate(Capture(openedAtUtc: Now), Plate, 0, Now));
        Assert.Equal(
            PhotoCaptureState.PhotosComingIn,
            PhotoCaptureStatus.Evaluate(Capture(openedAtUtc: Now, acceptedItemCount: 3, lastItemAtUtc: Now), Plate, 3, Now));
    }

    /// <summary>
    /// Paperwork alone is not photos coming in: the documents a hand-off's
    /// staff phone sends from its form leave the customer's link reading
    /// "Sent, not opened" until the customer opens it.
    /// </summary>
    [Fact]
    public void PaperworkAloneIsNotPhotosComingIn()
    {
        var paperworkOnly = Capture(PhotoCaptureOrigin.StandingForwarded, acceptedItemCount: 2, lastItemAtUtc: Now);

        Assert.Equal(PhotoCaptureState.SentNotOpened, PhotoCaptureStatus.Evaluate(paperworkOnly, Plate, 0, Now));
        Assert.Equal(PhotoCaptureState.Opened, PhotoCaptureStatus.Evaluate(paperworkOnly with { OpenedAtUtc = Now }, Plate, 0, Now));
        Assert.Equal(PhotoCaptureState.PhotosComingIn, PhotoCaptureStatus.Evaluate(paperworkOnly with { OpenedAtUtc = Now }, Plate, 1, Now));
    }

    [Fact]
    public void SendDecidesBetweenCompleteAndSomethingMissing()
    {
        var sent = Capture(openedAtUtc: Now, acceptedItemCount: 12, sentAtUtc: Now);

        Assert.Equal(PhotoCaptureState.Complete, PhotoCaptureStatus.Evaluate(sent, Nothing, 12, Now));
        Assert.Equal(PhotoCaptureState.SomethingMissing, PhotoCaptureStatus.Evaluate(sent, Plate, 12, Now));
    }

    [Fact]
    public void ARunOutLinkIsExpiredWithNoPhotosAndJudgedOnItsPhotosOtherwise()
    {
        var later = Now.AddDays(8);

        Assert.Equal(PhotoCaptureState.Expired, PhotoCaptureStatus.Evaluate(Capture(), Plate, 0, later));
        Assert.Equal(PhotoCaptureState.Complete, PhotoCaptureStatus.Evaluate(Capture(acceptedItemCount: 9), Nothing, 9, later));
    }

    [Fact]
    public void CancelledWinsOverEverything() =>
        Assert.Equal(
            PhotoCaptureState.Cancelled,
            PhotoCaptureStatus.Evaluate(Capture(sentAtUtc: Now, revokedAtUtc: Now), Nothing, 0, Now));

    [Fact]
    public void TwoRemindersWithNothingBackNeedsAPhoneCallUntilACallIsLogged()
    {
        var reminded = Capture(reminderCount: 2, lastRemindedAtUtc: Now.AddHours(-1));

        Assert.Equal(PhotoCaptureState.NeedsCall, PhotoCaptureStatus.Evaluate(reminded, Plate, 0, Now));
        Assert.Equal(
            PhotoCaptureState.SentNotOpened,
            PhotoCaptureStatus.Evaluate(reminded with { CallLoggedAtUtc = Now }, Plate, 0, Now));
        Assert.Equal(
            PhotoCaptureState.Opened,
            PhotoCaptureStatus.Evaluate(reminded with { OpenedAtUtc = Now }, Plate, 0, Now));
    }

    [Fact]
    public void NotOpenedIsDueAfterADayThenAfterTwo()
    {
        var created = Now.AddHours(-25);
        var capture = Capture(createdAtUtc: created);

        Assert.Equal("not_opened", PhotoCaptureStatus.DueReminder(capture, Plate, 0, Now)?.Code);
        Assert.Null(PhotoCaptureStatus.DueReminder(capture, Plate, 0, created.AddHours(23)));

        var onceReminded = capture with { ReminderCount = 1, LastRemindedAtUtc = Now };
        Assert.Null(PhotoCaptureStatus.DueReminder(onceReminded, Plate, 0, Now.AddHours(47)));
        Assert.Equal("not_opened", PhotoCaptureStatus.DueReminder(onceReminded, Plate, 0, Now.AddHours(49))?.Code);
    }

    [Fact]
    public void StartedButQuietForFourHoursIsDue()
    {
        var capture = Capture(createdAtUtc: Now.AddHours(-6), openedAtUtc: Now.AddHours(-5), acceptedItemCount: 4, lastItemAtUtc: Now.AddHours(-4).AddMinutes(-1));

        Assert.Equal("unfinished", PhotoCaptureStatus.DueReminder(capture, Plate, 4, Now)?.Code);
        Assert.Null(PhotoCaptureStatus.DueReminder(capture with { LastItemAtUtc = Now.AddMinutes(-30) }, Plate, 4, Now));
    }

    [Fact]
    public void SomethingMissingIsDueOnceAfterSendAndExpiryIsDueInsideTheLastDay()
    {
        var sent = Capture(openedAtUtc: Now, acceptedItemCount: 5, sentAtUtc: Now);
        Assert.Equal("something_missing", PhotoCaptureStatus.DueReminder(sent, Plate, 5, Now)?.Code);
        Assert.Null(PhotoCaptureStatus.DueReminder(sent with { LastRemindedAtUtc = Now.AddMinutes(1), ReminderCount = 1 }, Plate, 5, Now.AddMinutes(2)));

        // Photos still arriving (not quiet), a day from expiry: the expiry warning is due.
        var nearExpiry = Capture(
            createdAtUtc: Now.AddDays(-6).AddHours(-12),
            openedAtUtc: Now.AddDays(-6),
            acceptedItemCount: 3,
            lastItemAtUtc: Now.AddHours(-1));
        Assert.Equal("expiring", PhotoCaptureStatus.DueReminder(nearExpiry, Plate, 3, Now)?.Code);
    }

    [Fact]
    public void NothingIsDueForACompleteCancelledOrExpiredLink()
    {
        Assert.Null(PhotoCaptureStatus.DueReminder(Capture(sentAtUtc: Now, acceptedItemCount: 9), Nothing, 9, Now));
        Assert.Null(PhotoCaptureStatus.DueReminder(Capture(revokedAtUtc: Now), Plate, 0, Now));
        Assert.Null(PhotoCaptureStatus.DueReminder(Capture(), Plate, 0, Now.AddDays(8)));
    }

    /// <summary>
    /// Once its Case is archived a live link is dead: it reads Expired, is due
    /// no reminder and takes nothing more, whatever it held (operator,
    /// 9 November 2026; R793). A cancelled one stays Cancelled.
    /// </summary>
    [Fact]
    public void ALinkWhoseCaseIsArchivedReadsExpiredAndIsDueNothing()
    {
        var notOpened = Capture(createdAtUtc: Now.AddHours(-25));
        var sent = Capture(openedAtUtc: Now, acceptedItemCount: 5, sentAtUtc: Now);

        Assert.Equal(PhotoCaptureState.Expired, PhotoCaptureStatus.Evaluate(notOpened, Plate, 0, Now, caseArchived: true));
        Assert.Equal(PhotoCaptureState.Expired, PhotoCaptureStatus.Evaluate(sent, Plate, 5, Now, caseArchived: true));
        Assert.Equal(
            PhotoCaptureState.Cancelled,
            PhotoCaptureStatus.Evaluate(Capture(revokedAtUtc: Now), Plate, 0, Now, caseArchived: true));
        Assert.Null(PhotoCaptureStatus.DueReminder(notOpened, Plate, 0, Now, caseArchived: true));
        Assert.Null(PhotoCaptureStatus.DueReminder(sent, Plate, 5, Now, caseArchived: true));
        Assert.False(PhotoCaptureAccess.IsLive(notOpened, Now, caseArchived: true));
        Assert.True(PhotoCaptureAccess.IsLive(notOpened, Now));
    }

    [Fact]
    public void ACaptureWithNoCaseOrSentFromItsCaseHasNoPairing()
    {
        Assert.Equal(
            PhotoCapturePairing.None,
            PhotoCaptureStatus.Pairing(Capture(PhotoCaptureOrigin.StandingStaff, PhotoCaptureDestination.VehicleImages), recordChosenByStaff: null));
        Assert.Equal(PhotoCapturePairing.None, PhotoCaptureStatus.Pairing(Capture(), recordChosenByStaff: true));
    }

    [Fact]
    public void ACaptureThatFiledOnItsCaseWithNoRecordWasAlreadyInstructed() =>
        Assert.Equal(
            PhotoCapturePairing.FiledAsItArrived,
            PhotoCaptureStatus.Pairing(Capture(PhotoCaptureOrigin.Website), recordChosenByStaff: null));

    [Fact]
    public void ARecordPairedOnAStaffAssociationIsNotMatchedAutomatically()
    {
        var paired = Capture(PhotoCaptureOrigin.StandingForwarded) with { SubmissionGroupId = Guid.NewGuid() };

        Assert.Equal(PhotoCapturePairing.ChosenByStaff, PhotoCaptureStatus.Pairing(paired, recordChosenByStaff: true));
        Assert.Equal(PhotoCapturePairing.MatchedAutomatically, PhotoCaptureStatus.Pairing(paired, recordChosenByStaff: false));
        Assert.Equal(PhotoCapturePairing.MatchedAutomatically, PhotoCaptureStatus.Pairing(paired, recordChosenByStaff: null));
    }
}
