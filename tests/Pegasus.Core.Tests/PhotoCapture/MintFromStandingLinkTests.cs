using Pegasus.Core.ImageIntake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// A capture a standing link starts to take the photos now is Opened as it
/// is made, under a Session token. A hand-off is not Opened until the
/// customer opens it: the customer's link is a Handoff token, and the staff
/// phone gets a Session token of its own for the form's paperwork, so what it
/// still sends never marks the customer's link Opened.
/// </summary>
public sealed class MintFromStandingLinkTests
{
    private static readonly StandingCaptureForm Form =
        new("RY65 OFG", null, "Sam Taylor", null, null, PhotoCaptureDetails.Empty);

    [Fact]
    public async Task TakingThePhotosNowIsOpenedAsItIsMadeUnderASessionToken()
    {
        var store = new CreatingStore();

        var minted = await store.Mint().StartAsync(Link(), Form, CancellationToken.None);

        var created = Assert.Single(store.Created);
        Assert.Equal(Now, created.Capture.OpenedAtUtc);
        Assert.Equal(PhotoLinkTokenPurpose.Session, created.Purpose);
        Assert.Equal(PhotoLinkToken.Digest(minted.Token), created.Digest);
        Assert.Null(created.SessionDigest);
        Assert.Null(minted.SessionToken);
    }

    [Fact]
    public async Task AHandOffIsNotOpenedAndItsStaffPhoneGetsASessionTokenOfItsOwn()
    {
        var store = new CreatingStore();

        var minted = await store.Mint().HandoffAsync(Link(), Form, CancellationToken.None);

        var created = Assert.Single(store.Created);
        var session = Assert.IsType<CaptureLinkSecret>(minted.SessionToken);
        Assert.Null(created.Capture.OpenedAtUtc);
        Assert.Equal(PhotoCaptureOrigin.StandingForwarded, created.Capture.Origin);
        Assert.Equal(PhotoLinkTokenPurpose.Handoff, created.Purpose);
        Assert.Equal(PhotoLinkToken.Digest(minted.Token), created.Digest);
        Assert.Equal(PhotoLinkToken.Digest(session), created.SessionDigest);
        Assert.NotEqual(created.Digest, created.SessionDigest);
    }

    private static StandingPhotoLink Link() =>
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            StandingPhotoLinkKind.Contact,
            OrganizationId: Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            OrganizationName: "Example Bodyshop",
            PrincipalId: null,
            IsRepairer: true,
            ContactActive: true,
            Slug: "abcdefghjkmn",
            ForwardedMode: PhotoCaptureMode.Guided,
            LiveCameraOnly: false,
            LinkLifetimeDays: 7,
            OriginalsInBackground: true,
            PausedAtUtc: null,
            RetiredAtUtc: null,
            CreatedAtUtc: Now.AddDays(-30),
            Version: 1);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The capture store's create, kept; the routing and details the mint then records answer with the capture as made.</summary>
    private sealed class CreatingStore
    {
        private readonly AutomaticImageIntakeTests.Forwarder captures =
            AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);

        private readonly AutomaticImageIntakeTests.Forwarder links =
            AutomaticImageIntakeTests.Forwarder.For<IStandingPhotoLinkStore>(null);

        private readonly AutomaticImageIntakeTests.Forwarder candidates =
            AutomaticImageIntakeTests.Forwarder.For<IImageIntakeCaseCandidates>(null);

        private Core.PhotoCapture.PhotoCapture? current;

        public CreatingStore()
        {
            captures.Overrides[nameof(IPhotoCaptureStore.CreateAsync)] = args =>
            {
                var made = (NewPhotoCapture)args[0]!;
                Created.Add((made, (string)args[1]!, (PhotoLinkTokenPurpose)args[2]!, (string?)args[3]));
                current = Capture(made.Origin, made.Destination, openedAtUtc: made.OpenedAtUtc);
                return Task.FromResult(current);
            };
            captures.Overrides[nameof(IPhotoCaptureStore.RecordRoutingAsync)] = _ => Task.FromResult(current!);
            captures.Overrides[nameof(IPhotoCaptureStore.RecordDetailsAsync)] = _ => Task.FromResult(current!);
            links.Overrides[nameof(IStandingPhotoLinkStore.CountMintedSinceAsync)] = _ => Task.FromResult(0);
            candidates.Overrides[nameof(IImageIntakeCaseCandidates.FindEligibleByRegistrationAsync)] =
                _ => Task.FromResult<IReadOnlyList<ImageIntakeCaseCandidate>>([]);
        }

        public List<(NewPhotoCapture Capture, string Digest, PhotoLinkTokenPurpose Purpose, string? SessionDigest)> Created { get; } = [];

        public MintFromStandingLink Mint()
        {
            var store = (IPhotoCaptureStore)captures.Proxy;
            return new(
                store,
                (IStandingPhotoLinkStore)links.Proxy,
                new RecordPhotoCaptureDetails(store, new RoutePhotoCapture((IImageIntakeCaseCandidates)candidates.Proxy)),
                new FixedClock(Now));
        }
    }
}
