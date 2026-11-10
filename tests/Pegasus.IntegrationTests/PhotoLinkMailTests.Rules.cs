using Pegasus.Core.Operations;
using Pegasus.Web.Pages.PhotoCapture;

namespace Pegasus.IntegrationTests;

public sealed partial class PhotoLinkMailTests
{
    /// <summary>
    /// The e-mail greets the name after a leading title, and a title with a
    /// surname alone as typed (operator, 10 November 2026; R855).
    /// </summary>
    [Theory]
    [InlineData("Mr Leroy Ejiofor", "Leroy")]
    [InlineData("Mr Ejiofor", "Mr Ejiofor")]
    [InlineData("Sam Taylor", "Sam")]
    [InlineData(null, null)]
    public void TheMailGreetsTheNameAfterALeadingTitle(string? customerName, string? greeted) =>
        Assert.Equal(
            greeted,
            PhotoLinkMail.Values("RY65OFG", customerName, "QDOS", "https://example.invalid/c/link", null)[EmailTemplates.Claimant]);
}
