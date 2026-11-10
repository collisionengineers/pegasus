using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>Who a quick Case link is for is stored as a code of its own (R808), read back as the same value.</summary>
public sealed class PhotoCaptureQuickLinkCodesTests
{
    [Theory]
    [InlineData(PhotoCaptureQuickLinkFor.AccidentManagement, "accident_management")]
    [InlineData(PhotoCaptureQuickLinkFor.Bodyshop, "bodyshop")]
    public void EachQuickLinkTypeIsStoredAsItsCodeAndReadBack(PhotoCaptureQuickLinkFor quickLinkFor, string code)
    {
        Assert.Equal(code, PhotoCaptureCodes.ToCode(quickLinkFor));
        Assert.Equal(quickLinkFor, PhotoCaptureCodes.ParseQuickLinkFor(code));
    }

    [Fact]
    public void AnUnknownStoredCodeIsRefused() =>
        Assert.Throws<InvalidDataException>(() => PhotoCaptureCodes.ParseQuickLinkFor("claims_company"));
}
