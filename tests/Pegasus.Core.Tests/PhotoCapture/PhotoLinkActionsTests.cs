using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// What Send and Resend refuse, and in which words: a closed Case sends no
/// photo link (R847), and a dead link says whether it was cancelled or has
/// expired (R863). Resend counts as a reminder only when one was due, and Ask
/// for it on a capture not sent from a Case always counts (R914, R842).
/// </summary>
public sealed class PhotoLinkActionsTests
{
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.Parse("44444444-4444-4444-4444-444444444444"), [StaffRole.Administrator]);

    /// <summary>
    /// A closed Case cannot send a photo link, with the existing sentence
    /// (operator, 10 November 2026; R847), and no capture is made.
    /// </summary>
    [Fact]
    public async Task AClosedCaseIsRefusedALink()
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        var issue = new IssuePhotoLink(
            (IPhotoCaptureStore)captures.Proxy,
            Cases(Facts(isClosed: true)),
            new FixedClock(Now));

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => issue.ExecuteAsync(
            new IssuePhotoLinkRequest(
                CaseId,
                Staff,
                CustomerName: null,
                Mobile: null,
                Email: null,
                IncidentDate: null,
                PhotoCaptureMode.Guided,
                QuickLinkFor: null,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy),
            CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.CaseUnavailable, refusal.Code);
        Assert.Equal("The Case cannot take a photo link.", refusal.Message);
        Assert.Empty(captures.Calls);
    }

    /// <summary>
    /// Resend on a cancelled link says it was cancelled, even once
    /// it has also expired, since a cancelled link cannot be extended
    /// (operator, 10 November 2026; R863).
    /// </summary>
    [Fact]
    public async Task ACancelledLinkIsNotSentAgainAndSaysSo()
    {
        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Reissue(
            Capture(createdAtUtc: Now.AddDays(-8), revokedAtUtc: Now.AddDays(-2))));

        Assert.Equal(PhotoCaptureRefusals.Unavailable, refusal.Code);
        Assert.Equal("The link was cancelled; send a new one from the Case.", refusal.Message);
    }

    /// <summary>Resend on an expired link says to extend it first (R863).</summary>
    [Fact]
    public async Task AnExpiredLinkIsNotSentAgainUntilItIsExtended()
    {
        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Reissue(
            Capture(createdAtUtc: Now.AddDays(-8))));

        Assert.Equal(PhotoCaptureRefusals.Unavailable, refusal.Code);
        Assert.Equal("The link has expired; extend it first.", refusal.Message);
    }

    /// <summary>
    /// Resend on a Case link counts as a reminder when one is due: here the
    /// link has gone unopened for more than a day (R914).
    /// </summary>
    [Fact]
    public async Task ResendOnACaseLinkDueAReminderCountsAsOne()
    {
        var capture = Capture(createdAtUtc: Now.AddHours(-25));
        var captures = Store(capture);

        await Resend(captures, capture);

        Assert.Contains(nameof(IPhotoCaptureStore.AddTokenAsync), captures.Calls);
        Assert.Contains(nameof(IPhotoCaptureStore.RecordReminderAsync), captures.Calls);
    }

    /// <summary>
    /// Resend on a Case link with no reminder due makes a fresh link and does
    /// not count as a reminder, so it does not bring the phone call nearer (R914).
    /// </summary>
    [Fact]
    public async Task ResendOnACaseLinkWithNothingDueIsNotAReminder()
    {
        var capture = Capture(createdAtUtc: Now.AddHours(-1));
        var captures = Store(capture);

        await Resend(captures, capture);

        Assert.Contains(nameof(IPhotoCaptureStore.AddTokenAsync), captures.Calls);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.RecordReminderAsync), captures.Calls);
    }

    /// <summary>
    /// Ask for it on a Coming in capture, one not sent from a Case, always
    /// counts as a reminder, due or not (R842).
    /// </summary>
    [Fact]
    public async Task AskForItOnAComingInCaptureAlwaysCountsAsAReminder()
    {
        var capture = Capture(origin: PhotoCaptureOrigin.Website, createdAtUtc: Now.AddHours(-1));
        Assert.Null(PhotoCaptureStatus.DueReminder(Row(capture), Now));
        var captures = Store(capture);

        await Resend(captures, capture);

        Assert.Contains(nameof(IPhotoCaptureStore.RecordReminderAsync), captures.Calls);
    }

    private static readonly Guid CaseId = Capture().CaseId!.Value;

    private static Task<IssuedPhotoLink> Reissue(Core.PhotoCapture.PhotoCapture capture)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.GetAsync)] =
            _ => Task.FromResult<Core.PhotoCapture.PhotoCapture?>(capture);
        return Actions(captures, capture).ReissueAsync(
            Staff, capture.Id, PhotoLinkTokenPurpose.Copy, isReminder: false, "reissue-operation", CancellationToken.None);
    }

    private static Task<IssuedPhotoLink> Resend(AutomaticImageIntakeTests.Forwarder captures, Core.PhotoCapture.PhotoCapture capture) =>
        Actions(captures, capture).ResendAsync(
            Staff, capture.Id, PhotoLinkTokenPurpose.Copy, "resend-operation", CancellationToken.None);

    /// <summary>A store that holds the one capture, takes its new token and records a reminder on it.</summary>
    private static AutomaticImageIntakeTests.Forwarder Store(Core.PhotoCapture.PhotoCapture capture)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.GetAsync)] =
            _ => Task.FromResult<Core.PhotoCapture.PhotoCapture?>(capture);
        captures.Overrides[nameof(IPhotoCaptureStore.AddTokenAsync)] = _ => Task.CompletedTask;
        captures.Overrides[nameof(IPhotoCaptureStore.RecordReminderAsync)] =
            _ => Task.FromResult(capture with { ReminderCount = capture.ReminderCount + 1, LastRemindedAtUtc = Now });
        return captures;
    }

    private static PhotoLinkActions Actions(AutomaticImageIntakeTests.Forwarder captures, Core.PhotoCapture.PhotoCapture capture)
    {
        var queries = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureQueries>(null);
        queries.Overrides[nameof(IPhotoCaptureQueries.GetRowAsync)] =
            _ => Task.FromResult<PhotoCaptureListRow?>(Row(capture));
        return new PhotoLinkActions(
            (IPhotoCaptureStore)captures.Proxy,
            Cases(Facts(isClosed: false)),
            (IPhotoCaptureQueries)queries.Proxy,
            new FixedClock(Now));
    }

    private static PhotoCaptureListRow Row(Core.PhotoCapture.PhotoCapture capture) =>
        new(
            capture,
            OrganizationName: null,
            ImageIntakeReference: null,
            ImageIntakeId: null,
            MissingItems: [],
            IssuedByName: null,
            PhotoCapturePairing.None,
            CaseArchived: false,
            Photographs: 0,
            SentOnToCustomer: false);

    private static IPhotoCaptureCaseReader Cases(PhotoCaptureCaseFacts facts)
    {
        var cases = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureCaseReader>(null);
        cases.Overrides[nameof(IPhotoCaptureCaseReader.GetAsync)] =
            _ => Task.FromResult<PhotoCaptureCaseFacts?>(facts);
        return (IPhotoCaptureCaseReader)cases.Proxy;
    }

    private static PhotoCaptureCaseFacts Facts(bool isClosed) =>
        new(
            CaseId,
            "QDOS26001",
            "RY65OFG",
            IncidentDate: null,
            ClaimantName: "Sam Taylor",
            ClaimantContactNumber: null,
            PrincipalName: "QDOS",
            PrincipalId: null,
            PrincipalOrganizationId: null,
            IsArchived: false,
            IsTriage: false,
            isClosed);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
