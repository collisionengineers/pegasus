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
/// Cancel, Extend and Log call are taken only where the link offers them,
/// and a repeated operation key is answered as done (#2130; R1092).
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
                PhotoLinkTokenPurpose.Copy,
                "closed-case-operation"),
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

        Assert.Contains(nameof(IPhotoCaptureStore.ReissueAsync), captures.Calls);
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

        Assert.Contains(nameof(IPhotoCaptureStore.ReissueAsync), captures.Calls);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.RecordReminderAsync), captures.Calls);
    }

    /// <summary>
    /// Ask for it is retired (v51 Q; R948): a capture a standing or website
    /// link started is never sent again, so no token is minted for it.
    /// </summary>
    [Fact]
    public async Task AStandingOrWebsiteCaptureIsNeverSentAgain()
    {
        var capture = Capture(origin: PhotoCaptureOrigin.Website, createdAtUtc: Now.AddHours(-1));
        var captures = Store(capture);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Resend(captures, capture));

        Assert.DoesNotContain(nameof(IPhotoCaptureStore.ReissueAsync), captures.Calls);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.RecordReminderAsync), captures.Calls);
    }

    /// <summary>
    /// The Send form posted again under its operation key makes no second
    /// link: the store answers the capture the key made, and Core hands back
    /// no token for it, since only the first post holds one.
    /// </summary>
    [Fact]
    public async Task OneOperationKeyIssuesOneLink()
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        var byKey = new Dictionary<string, Core.PhotoCapture.PhotoCapture>(StringComparer.Ordinal);
        var digests = new List<string>();
        captures.Overrides[nameof(IPhotoCaptureStore.IssueAsync)] = args =>
        {
            var key = (string)args[4]!;
            if (byKey.TryGetValue(key, out var made))
            {
                return Task.FromResult((made, false));
            }

            digests.Add((string)args[1]!);
            made = Capture() with { Id = ((NewPhotoCapture)args[0]!).Id };
            byKey[key] = made;
            return Task.FromResult((made, true));
        };
        var issue = new IssuePhotoLink((IPhotoCaptureStore)captures.Proxy, Cases(Facts(isClosed: false)), new FixedClock(Now));

        var first = await issue.ExecuteAsync(IssueRequest("send-operation"), CancellationToken.None);
        var again = await issue.ExecuteAsync(IssueRequest("send-operation"), CancellationToken.None);
        var other = await issue.ExecuteAsync(IssueRequest("another-send"), CancellationToken.None);

        Assert.False(first.Replayed);
        Assert.Equal(PhotoLinkToken.Digest(first.Token), digests[0]);
        Assert.True(again.Replayed);
        Assert.Equal(first.Capture.Id, again.Capture.Id);
        Assert.Null(again.Token.Value);
        Assert.False(other.Replayed);
        Assert.NotEqual(first.Capture.Id, other.Capture.Id);
        Assert.Equal(2, digests.Count);
    }

    /// <summary>
    /// Resend posted again under its operation key makes no second token and
    /// counts no second reminder (R914).
    /// </summary>
    [Fact]
    public async Task OneOperationKeyResendsOnce()
    {
        var capture = Capture(createdAtUtc: Now.AddHours(-25));
        var captures = Store(capture);

        var first = await Resend(captures, capture);
        var again = await Resend(captures, capture);

        Assert.False(first.Replayed);
        Assert.NotNull(first.Token.Value);
        Assert.True(again.Replayed);
        Assert.Null(again.Token.Value);
        Assert.Equal(2, captures.Calls.Count(call => call == nameof(IPhotoCaptureStore.ReissueAsync)));
        Assert.Single(captures.Calls, call => call == nameof(IPhotoCaptureStore.RecordReminderAsync));
    }

    /// <summary>
    /// The acts a link offers, the one rule its page, its handlers and the
    /// photo link tools follow (#2130; R1092): a live Case link offers all
    /// four; a cancelled one none; an expired one Log call and Extend; one
    /// whose Case is archived Log call alone (R793); a live website capture
    /// Extend and Cancel, since only a Case link is sent again or called.
    /// </summary>
    [Fact]
    public void EachStateOfALinkOffersItsOwnActs()
    {
        Assert.Equal(
            new PhotoLinkOffers(Resend: true, LogCall: true, Extend: true, Cancel: true),
            PhotoLinkOffers.For(Row(Capture()), Now));
        Assert.Equal(
            new PhotoLinkOffers(Resend: false, LogCall: false, Extend: false, Cancel: false),
            PhotoLinkOffers.For(Row(Capture(revokedAtUtc: Now.AddMinutes(-5))), Now));
        Assert.Equal(
            new PhotoLinkOffers(Resend: false, LogCall: true, Extend: true, Cancel: false),
            PhotoLinkOffers.For(Row(Capture(createdAtUtc: Now.AddDays(-8))), Now));
        Assert.Equal(
            new PhotoLinkOffers(Resend: false, LogCall: true, Extend: false, Cancel: false),
            PhotoLinkOffers.For(Row(Capture(), caseArchived: true), Now));
        Assert.Equal(
            new PhotoLinkOffers(Resend: false, LogCall: false, Extend: true, Cancel: true),
            PhotoLinkOffers.For(Row(Capture(origin: PhotoCaptureOrigin.Website)), Now));
    }

    /// <summary>A cancelled link is not extended: the act is refused as not offered and the store is not asked (R1092).</summary>
    [Fact]
    public async Task ACancelledLinkIsNotExtended()
    {
        var capture = Capture(revokedAtUtc: Now.AddMinutes(-5));
        var captures = ActStore(capture);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Actions(captures, capture).ExtendAsync(
            Staff, capture.Id, CaptureLimits.DefaultLifetimeDays, "extend-operation", CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.NotOffered, refusal.Code);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.ExtendAsync), captures.Calls);
    }

    /// <summary>An expired link is not cancelled: it no longer takes photos (R1092).</summary>
    [Fact]
    public async Task AnExpiredLinkIsNotCancelled()
    {
        var capture = Capture(createdAtUtc: Now.AddDays(-8));
        var captures = ActStore(capture);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Actions(captures, capture).RevokeAsync(
            Staff, capture.Id, "cancel-operation", CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.NotOffered, refusal.Code);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.RevokeAsync), captures.Calls);
    }

    /// <summary>No call is logged on a cancelled link (R1092).</summary>
    [Fact]
    public async Task NoCallIsLoggedOnACancelledLink()
    {
        var capture = Capture(revokedAtUtc: Now.AddMinutes(-5));
        var captures = ActStore(capture);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Actions(captures, capture).LogCallAsync(
            Staff, capture.Id, "call-operation", CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.NotOffered, refusal.Code);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.RecordCallAsync), captures.Calls);
    }

    /// <summary>
    /// A repeat is answered as done: an operation key that already recorded
    /// the act goes to the store, which replays it, even once the link no
    /// longer offers the act (operator, 17 November 2026; R1092).
    /// </summary>
    [Fact]
    public async Task ARepeatedKeyIsAnsweredAsDoneOnceTheLinkHasMovedOn()
    {
        var cancelled = Capture(revokedAtUtc: Now.AddMinutes(-5));
        var cancelledAgain = ActStore(cancelled, (PhotoLinkActs.Cancelled, "cancel-operation"));
        var extendedAgain = ActStore(cancelled, (PhotoLinkActs.Extended, "extend-operation"));

        await Actions(cancelledAgain, cancelled).RevokeAsync(Staff, cancelled.Id, "cancel-operation", CancellationToken.None);
        await Actions(extendedAgain, cancelled).ExtendAsync(
            Staff, cancelled.Id, CaptureLimits.DefaultLifetimeDays, "extend-operation", CancellationToken.None);

        Assert.Contains(nameof(IPhotoCaptureStore.RevokeAsync), cancelledAgain.Calls);
        Assert.Contains(nameof(IPhotoCaptureStore.ExtendAsync), extendedAgain.Calls);
    }

    /// <summary>An act the link offers goes straight to the store, with no read of the key's acts.</summary>
    [Fact]
    public async Task AnOfferedActIsTakenWithoutReadingTheKeysActs()
    {
        var capture = Capture();
        var captures = ActStore(capture);

        await Actions(captures, capture).RevokeAsync(Staff, capture.Id, "cancel-operation", CancellationToken.None);

        Assert.Contains(nameof(IPhotoCaptureStore.RevokeAsync), captures.Calls);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.HasRecordedActAsync), captures.Calls);
    }

    /// <summary>
    /// Extend on a link whose Case is archived still says the Case cannot
    /// take a photo link (R793, R847), before the offers are asked.
    /// </summary>
    [Fact]
    public async Task ALinkWhoseCaseIsArchivedStillSaysTheCaseCannotTakeALink()
    {
        var capture = Capture();
        var captures = ActStore(capture);

        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Actions(captures, capture, caseArchived: true).ExtendAsync(
            Staff, capture.Id, CaptureLimits.DefaultLifetimeDays, "extend-operation", CancellationToken.None));

        Assert.Equal(PhotoCaptureRefusals.CaseUnavailable, refusal.Code);
        Assert.Equal("The Case cannot take a photo link.", refusal.Message);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.HasRecordedActAsync), captures.Calls);
    }

    /// <summary>
    /// Only a member of staff is kept as a link's issuer (#2147): the
    /// Automation Actor's subject is never taken for a staff id, even when it
    /// reads as one.
    /// </summary>
    [Fact]
    public async Task OnlyAMemberOfStaffIsKeptAsTheIssuer()
    {
        var automation = ActionActor.Automation("55555555-5555-5555-5555-555555555555");

        Assert.Null(await IssuerAsync(automation));
        Assert.Equal(Guid.Parse(Staff.SubjectId), await IssuerAsync(Staff));
    }

    private static async Task<Guid?> IssuerAsync(ActionActor actor)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        NewPhotoCapture? issued = null;
        captures.Overrides[nameof(IPhotoCaptureStore.IssueAsync)] = args =>
        {
            issued = (NewPhotoCapture)args[0]!;
            return Task.FromResult((Capture() with { Id = issued.Id }, true));
        };
        var issue = new IssuePhotoLink((IPhotoCaptureStore)captures.Proxy, Cases(Facts(isClosed: false)), new FixedClock(Now));

        await issue.ExecuteAsync(IssueRequest("issuer-operation") with { Actor = actor }, CancellationToken.None);

        Assert.NotNull(issued);
        return issued.IssuedByStaffId;
    }

    /// <summary>
    /// A store holding the one capture that takes each act; <paramref name="recorded"/>
    /// is the act an operation key already recorded on it, if any.
    /// </summary>
    private static AutomaticImageIntakeTests.Forwarder ActStore(
        Core.PhotoCapture.PhotoCapture capture,
        (string Act, string Key)? recorded = null)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.HasRecordedActAsync)] =
            args => Task.FromResult(recorded == ((string)args[1]!, (string)args[2]!));
        captures.Overrides[nameof(IPhotoCaptureStore.RevokeAsync)] = _ => Task.FromResult(capture);
        captures.Overrides[nameof(IPhotoCaptureStore.ExtendAsync)] = _ => Task.FromResult(capture);
        captures.Overrides[nameof(IPhotoCaptureStore.RecordCallAsync)] = _ => Task.FromResult(capture);
        return captures;
    }

    private static IssuePhotoLinkRequest IssueRequest(string operationKey) =>
        new(
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
            PhotoLinkTokenPurpose.Copy,
            operationKey);

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
        // One token per operation key, as the store keeps it.
        var reissued = new HashSet<string>(StringComparer.Ordinal);
        captures.Overrides[nameof(IPhotoCaptureStore.ReissueAsync)] =
            args => Task.FromResult(reissued.Add((string)args[4]!));
        captures.Overrides[nameof(IPhotoCaptureStore.RecordReminderAsync)] =
            _ => Task.FromResult(capture with { ReminderCount = capture.ReminderCount + 1, LastRemindedAtUtc = Now });
        return captures;
    }

    private static PhotoLinkActions Actions(
        AutomaticImageIntakeTests.Forwarder captures,
        Core.PhotoCapture.PhotoCapture capture,
        bool caseArchived = false)
    {
        var queries = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureQueries>(null);
        queries.Overrides[nameof(IPhotoCaptureQueries.GetRowAsync)] =
            _ => Task.FromResult<PhotoCaptureListRow?>(Row(capture, caseArchived));
        return new PhotoLinkActions(
            (IPhotoCaptureStore)captures.Proxy,
            Cases(Facts(isClosed: false, caseArchived)),
            (IPhotoCaptureQueries)queries.Proxy,
            new FixedClock(Now));
    }

    private static PhotoCaptureListRow Row(Core.PhotoCapture.PhotoCapture capture, bool caseArchived = false) =>
        new(
            capture,
            OrganizationName: null,
            ImageIntakeReference: null,
            ImageIntakeId: null,
            MissingItems: [],
            IssuedByName: null,
            CaseArchived: caseArchived,
            Photographs: 0,
            SentOnToCustomer: false);

    private static IPhotoCaptureCaseReader Cases(PhotoCaptureCaseFacts facts)
    {
        var cases = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureCaseReader>(null);
        cases.Overrides[nameof(IPhotoCaptureCaseReader.GetAsync)] =
            _ => Task.FromResult<PhotoCaptureCaseFacts?>(facts);
        return (IPhotoCaptureCaseReader)cases.Proxy;
    }

    private static PhotoCaptureCaseFacts Facts(bool isClosed, bool isArchived = false) =>
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
            IsArchived: isArchived,
            IsTriage: false,
            isClosed);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
