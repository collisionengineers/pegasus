using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// What Send, Resend and Remind refuse, and in which words: a closed Case
/// sends no photo link (R847), and a dead link says whether it was cancelled
/// or has expired (R863).
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
    /// Resend or Remind on a cancelled link says it was cancelled, even once
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

    /// <summary>Resend or Remind on an expired link says to extend it first (R863).</summary>
    [Fact]
    public async Task AnExpiredLinkIsNotSentAgainUntilItIsExtended()
    {
        var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(() => Reissue(
            Capture(createdAtUtc: Now.AddDays(-8))));

        Assert.Equal(PhotoCaptureRefusals.Unavailable, refusal.Code);
        Assert.Equal("The link has expired; extend it first.", refusal.Message);
    }

    private static readonly Guid CaseId = Capture().CaseId!.Value;

    private static Task<IssuedPhotoLink> Reissue(Core.PhotoCapture.PhotoCapture capture)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.GetAsync)] =
            _ => Task.FromResult<Core.PhotoCapture.PhotoCapture?>(capture);
        var actions = new PhotoLinkActions(
            (IPhotoCaptureStore)captures.Proxy,
            Cases(Facts(isClosed: false)),
            new FixedClock(Now));
        return actions.ReissueAsync(
            Staff, capture.Id, PhotoLinkTokenPurpose.Copy, isReminder: false, "reissue-operation", CancellationToken.None);
    }

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
