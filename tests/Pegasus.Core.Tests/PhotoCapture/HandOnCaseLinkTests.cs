using Pegasus.Core.ImageIntake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// "Send customer a link" on a quick link sent from a Case hands the same
/// capture on (R798): what the firm typed is recorded, the staff phone gets a
/// Session token before the customer's Handoff token is written, and nothing
/// stored is rewritten, so the firm's Opened stands. Only a live quick Case
/// link that names who it is for is handed on.
/// </summary>
public sealed class HandOnCaseLinkTests
{
    private static readonly StandingCaptureForm Form =
        new("AB12CDE", null, "Sam Taylor", null, null, new PhotoCaptureDetails(Handler: "Not sure yet"));

    [Fact]
    public async Task AQuickCaseLinkIsHandedOnWithASessionTokenThenTheCustomersHandoffToken()
    {
        var opened = QuickCaseLink(PhotoCaptureQuickLinkFor.Bodyshop) with { OpenedAtUtc = Now.AddMinutes(-5) };
        var store = new TokenStore(opened);

        var handedOn = await store.HandOn().ExecuteAsync(opened, Form, CancellationToken.None);

        var session = Assert.IsType<CaptureLinkSecret>(handedOn.SessionToken);
        Assert.Equal(
            new[] { (PhotoLinkToken.Digest(session), PhotoLinkTokenPurpose.Session), (PhotoLinkToken.Digest(handedOn.Token), PhotoLinkTokenPurpose.Handoff) },
            store.Tokens);
        // The Case's registration stands, whatever the form held; the firm's Opened is not rewritten.
        Assert.Equal("RY65OFG", Assert.Single(store.Details));
        Assert.Equal(opened.OpenedAtUtc, handedOn.Capture.OpenedAtUtc);
        Assert.StartsWith("Hi Sam, Collision Engineers need a few photos of RY65 OFG", handedOn.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IPhotoCaptureStore.RecordOpenedAsync), store.Calls);
    }

    [Fact]
    public async Task OnlyALiveQuickCaseLinkThatNamesWhoItIsForIsHandedOn()
    {
        var guided = Capture();
        var standing = Capture(PhotoCaptureOrigin.StandingStaff) with { Mode = PhotoCaptureMode.Quick };
        var unnamed = Capture() with { Mode = PhotoCaptureMode.Quick };
        var cancelled = QuickCaseLink(PhotoCaptureQuickLinkFor.AccidentManagement) with { RevokedAtUtc = Now };

        foreach (var capture in new[] { guided, standing, unnamed, cancelled })
        {
            var store = new TokenStore(capture);
            var refusal = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(
                () => store.HandOn().ExecuteAsync(capture, Form, CancellationToken.None));

            Assert.Equal(PhotoCaptureRefusals.Unavailable, refusal.Code);
            Assert.Empty(store.Tokens);
            Assert.Empty(store.Details);
        }

        Assert.True(HandOnCaseLink.IsQuickCaseLink(QuickCaseLink(PhotoCaptureQuickLinkFor.AccidentManagement)));
    }

    private static Core.PhotoCapture.PhotoCapture QuickCaseLink(PhotoCaptureQuickLinkFor quickLinkFor) =>
        Capture() with { Mode = PhotoCaptureMode.Quick, QuickLinkFor = quickLinkFor };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The capture store's token and details writes, kept in order; any other call is not expected.</summary>
    private sealed class TokenStore
    {
        private readonly AutomaticImageIntakeTests.Forwarder captures =
            AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);

        private readonly AutomaticImageIntakeTests.Forwarder candidates =
            AutomaticImageIntakeTests.Forwarder.For<IImageIntakeCaseCandidates>(null);

        public TokenStore(Core.PhotoCapture.PhotoCapture capture)
        {
            captures.Overrides[nameof(IPhotoCaptureStore.AddTokenAsync)] = args =>
            {
                Tokens.Add(((string)args[1]!, (PhotoLinkTokenPurpose)args[2]!));
                return Task.CompletedTask;
            };
            captures.Overrides[nameof(IPhotoCaptureStore.RecordDetailsAsync)] = args =>
            {
                Details.Add((string?)args[2]);
                return Task.FromResult(capture with { Version = capture.Version + 1 });
            };
        }

        public List<(string Digest, PhotoLinkTokenPurpose Purpose)> Tokens { get; } = [];

        public List<string?> Details { get; } = [];

        public List<string> Calls => captures.Calls;

        public HandOnCaseLink HandOn()
        {
            var store = (IPhotoCaptureStore)captures.Proxy;
            return new(
                store,
                new RecordPhotoCaptureDetails(store, new RoutePhotoCapture((IImageIntakeCaseCandidates)candidates.Proxy)),
                new FixedClock(Now));
        }
    }
}
