using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// A presented token resolves with its purpose, and only a Session token, the
/// phone that made the capture, never records the link Opened: a hand-off's
/// staff phone sending the form's paperwork does not open the customer's link.
/// </summary>
public sealed class PhotoCaptureAccessTests
{
    [Theory]
    [InlineData(PhotoLinkTokenPurpose.Email, true)]
    [InlineData(PhotoLinkTokenPurpose.Copy, true)]
    [InlineData(PhotoLinkTokenPurpose.Handoff, true)]
    [InlineData(PhotoLinkTokenPurpose.Session, false)]
    public void OnlyASessionTokenNeverOpensTheLink(PhotoLinkTokenPurpose purpose, bool marksOpened) =>
        Assert.Equal(marksOpened, PhotoCaptureAccess.MarksOpened(purpose));

    [Fact]
    public async Task AResolvedTokenCarriesItsPurpose()
    {
        var session = PhotoLinkToken.Create();
        var store = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        store.Overrides[nameof(IPhotoCaptureStore.FindByTokenDigestAsync)] = args =>
            Task.FromResult<(Core.PhotoCapture.PhotoCapture Capture, bool TokenRevoked, bool CaseArchived, PhotoLinkTokenPurpose Purpose)?>(
                string.Equals((string)args[0]!, PhotoLinkToken.Digest(session), StringComparison.Ordinal)
                    ? (Capture(PhotoCaptureOrigin.StandingForwarded), false, false, PhotoLinkTokenPurpose.Session)
                    : null);
        // No standing link store is given: a token never reads one.
        var access = new PhotoCaptureAccess((IPhotoCaptureStore)store.Proxy, null!, new FixedClock(Now));

        var resolved = await access.ResolveTokenAsync(session.Value, CancellationToken.None);
        var unknown = await access.ResolveTokenAsync(PhotoLinkToken.Create().Value, CancellationToken.None);

        Assert.NotNull(resolved.Capture);
        Assert.Equal(PhotoLinkTokenPurpose.Session, resolved.TokenPurpose);
        Assert.False(PhotoCaptureAccess.MarksOpened(resolved.TokenPurpose));
        Assert.False(unknown.IsAvailable);
        Assert.Null(unknown.TokenPurpose);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
