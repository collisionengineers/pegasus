using Pegasus.Web.Pages.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>The photo link e-mail's subject (FRD-28 The photo link e-mail; R788).</summary>
public sealed partial class PhotoLinkMailTests
{
    [Fact]
    public void TheSubjectNamesTheVehicleForCollisionEngineers() =>
        Assert.Equal("Photos of AB12 CDE for Collision Engineers", PhotoLinkMail.Subject("AB12CDE"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutARegistrationTheSubjectStillSaysWhatTheMailIsFor(string? registration) =>
        Assert.Equal("Photos for Collision Engineers", PhotoLinkMail.Subject(registration));
}
