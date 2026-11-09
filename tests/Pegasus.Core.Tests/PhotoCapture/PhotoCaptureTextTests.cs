using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoCaptureTextTests
{
    private static readonly string[] KnownZones = ["fd", "rear"];

    [Theory]
    [InlineData("ry65 ofg", "RY65OFG")]
    [InlineData(" RY65-OFG ", "RY65OFG")]
    [InlineData("A", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("ABCDEFGHIJK", null)]
    public void ARegistrationIsUppercaseLettersAndDigitsBetweenTwoAndTenLong(string? typed, string? expected) =>
        Assert.Equal(expected, PhotoCaptureText.Registration(typed));

    [Theory]
    [InlineData("RY65OFG", "RY65 OFG")]
    [InlineData("A1", "A1")]
    [InlineData("ABC123D", "ABC123D")]
    public void ACurrentFormatPlateShowsWithItsSpace(string normalized, string shown) =>
        Assert.Equal(shown, PhotoCaptureText.DisplayRegistration(normalized));

    [Fact]
    public void TypedTextIsTrimmedBoundedAndStrippedOfControlCharacters()
    {
        Assert.Null(PhotoCaptureText.Name("   "));
        Assert.Equal("Sam Taylor", PhotoCaptureText.Name(" Sam\u0007 Taylor "));
        Assert.Equal(CaptureLimits.NoteLength, PhotoCaptureText.Note(new string('n', CaptureLimits.NoteLength + 50))!.Length);
        Assert.Null(PhotoCaptureText.Email("not an address"));
        Assert.Equal("sam@example.com", PhotoCaptureText.Email(" sam@example.com "));
    }

    [Fact]
    public void DetailsKeepOnlyKnownZonesAndSensibleMileage()
    {
        var details = PhotoCaptureText.Details(new PhotoCaptureDetails(
            Mileage: 48_210,
            DamageAreas: ["fd", "nonsense", "fd", "rear"]));

        Assert.Equal(KnownZones, details.MarkedAreas);
        Assert.Equal(48_210, details.Mileage);
        Assert.Null(PhotoCaptureText.Details(new PhotoCaptureDetails(Mileage: 0)).Mileage);
    }

    [Fact]
    public void DetailsKeepTheNoKeysSkip()
    {
        Assert.True(PhotoCaptureText.Details(new PhotoCaptureDetails(NoKeys: true)).NoKeys);
        Assert.False(PhotoCaptureText.Details(new PhotoCaptureDetails()).NoKeys);
    }

    [Fact]
    public void ANoteEmptiedBeforeSendIsStoredEmptyAndNoNoteKeepsTheEarlierOne()
    {
        Assert.Null(PhotoCaptureText.NoteOnSend("", "The boot doesn't close."));
        Assert.Null(PhotoCaptureText.NoteOnSend("   ", "The boot doesn't close."));
        Assert.Equal("The boot doesn't close.", PhotoCaptureText.NoteOnSend(null, "The boot doesn't close."));
        Assert.Equal("Also the bonnet.", PhotoCaptureText.NoteOnSend(" Also the bonnet. ", "The boot doesn't close."));
    }

    [Fact]
    public void TheHandoffMessageGreetsByFirstNameWhenOneIsKnown()
    {
        Assert.StartsWith("Hi Sam, Collision Engineers need a few photos of RY65 OFG", PhotoCaptureMessages.Handoff("Sam Taylor", "RY65OFG"), StringComparison.Ordinal);
        Assert.StartsWith("Hi, Collision Engineers need", PhotoCaptureMessages.Handoff(null, "RY65OFG"), StringComparison.Ordinal);
    }
}
