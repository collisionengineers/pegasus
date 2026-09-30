using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// A retained e-mail is named by its subject (operator, 30 September 2026), not
/// by the hash of its message id.
/// </summary>
public sealed class EmailSourceFormatTests
{
    [Theory]
    [InlineData("QDOS instruction QDOS26001", "QDOS instruction QDOS26001.eml")]
    [InlineData("  Padded subject  ", "Padded subject.eml")]
    public void ARetainedMessageIsNamedByItsTrimmedSubject(string subject, string expected)
    {
        Assert.Equal(expected, EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("<>|:*?\"/\\")]
    public void AMessageWithNoUsableSubjectIsUntitled(string? subject)
    {
        Assert.Equal("Message.eml", EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Theory]
    [InlineData("RE: Claim 12/34 for <Mr Smith>", "RE Claim 1234 for Mr Smith.eml")]
    [InlineData("Where is the car?*", "Where is the car.eml")]
    [InlineData("C:\\path\\to|file", "Cpathtofile.eml")]
    [InlineData("Vehicle's \"report\"; final", "Vehicles report final.eml")]
    [InlineData("Line one\r\nline two", "Line oneline two.eml")]
    public void CharactersAFileNameRefusesAreRemoved(string subject, string expected)
    {
        Assert.Equal(expected, EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Fact]
    public void ALongSubjectIsCutSoTheNameStaysInsideEveryCustodyLimit()
    {
        var name = EmailSourceFormat.RetainedMessageFileName(new string('a', 400));

        Assert.Equal(EmailSourceFormat.MaximumNameSubjectLength + ".eml".Length, name.Length);
        Assert.EndsWith(".eml", name, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCutNeverLeavesTrailingSpaceOrHalfASurrogatePair()
    {
        var spaced = new string('a', EmailSourceFormat.MaximumNameSubjectLength - 1) + "   tail";
        var paired = new string('a', EmailSourceFormat.MaximumNameSubjectLength - 1) + "\U0001F697 tail";

        Assert.Equal(
            new string('a', EmailSourceFormat.MaximumNameSubjectLength - 1) + ".eml",
            EmailSourceFormat.RetainedMessageFileName(spaced));
        Assert.Equal(
            new string('a', EmailSourceFormat.MaximumNameSubjectLength - 1) + ".eml",
            EmailSourceFormat.RetainedMessageFileName(paired));
    }

    /// <summary>
    /// A download saved as <c>CON.eml</c> fails or opens the device on Windows, so a
    /// subject that is a device name, in any case and with or without an
    /// extension, trailing dots or trailing spaces, is untitled.
    /// </summary>
    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("Prn")]
    [InlineData("AUX")]
    [InlineData("nul")]
    [InlineData("COM1")]
    [InlineData("Com5")]
    [InlineData("com9")]
    [InlineData("LPT1")]
    [InlineData("lpt9")]
    [InlineData("COM¹")]
    [InlineData("LPT³")]
    [InlineData("CONIN$")]
    [InlineData("conout$")]
    [InlineData("CON.")]
    [InlineData("con. .")]
    [InlineData("  aux  ")]
    [InlineData("NUL.txt")]
    [InlineData("nul .txt")]
    [InlineData("Com3.log.")]
    public void AWindowsDeviceNameIsNeverUsedAsTheStem(string subject)
    {
        Assert.Equal("Message.eml", EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Theory]
    [InlineData("COM10", "COM10.eml")]
    [InlineData("LPT0", "LPT0.eml")]
    [InlineData("CONSOLE", "CONSOLE.eml")]
    [InlineData("Console.log", "Console.log.eml")]
    [InlineData("CON Claim", "CON Claim.eml")]
    [InlineData("Nullify", "Nullify.eml")]
    [InlineData("The CON", "The CON.eml")]
    [InlineData(".hidden", ".hidden.eml")]
    public void ASubjectThatOnlyStartsWithOrContainsADeviceNameKeepsItsName(string subject, string expected)
    {
        Assert.Equal(expected, EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Theory]
    [InlineData("Claim. ", "Claim.eml")]
    [InlineData("Claim...", "Claim.eml")]
    [InlineData("Claim . .", "Claim.eml")]
    [InlineData("Claim. ", "Claim.eml")]
    [InlineData("Re: Claim.", "Re Claim.eml")]
    [InlineData(". . .", "Message.eml")]
    public void TheStemNeverEndsInADotOrASpaceBeforeTheExtension(string subject, string expected)
    {
        var name = EmailSourceFormat.RetainedMessageFileName(subject);

        Assert.Equal(expected, name);
        var stem = name[..^".eml".Length];
        Assert.False(stem.EndsWith('.') || stem.EndsWith(' '), $"'{name}' ends its stem in a dot or a space.");
    }

    [Fact]
    public void ACutThatLandsOnADotLeavesNoDotBeforeTheExtension()
    {
        var subject = new string('a', EmailSourceFormat.MaximumNameSubjectLength - 1) + ".tail";

        Assert.Equal(
            new string('a', EmailSourceFormat.MaximumNameSubjectLength - 1) + ".eml",
            EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Fact]
    public void ACutThatLeavesADeviceNameFallsBackToUntitled()
    {
        var subject = "CON" + new string(' ', EmailSourceFormat.MaximumNameSubjectLength) + "tail";

        Assert.Equal("Message.eml", EmailSourceFormat.RetainedMessageFileName(subject));
    }

    [Fact]
    public void TwoMessagesWithOneSubjectShareOneName()
    {
        Assert.Equal(
            EmailSourceFormat.RetainedMessageFileName("Instruction"),
            EmailSourceFormat.RetainedMessageFileName("Instruction"));
    }

    [Fact]
    public void ANameIsRecognisedAsAnEmail()
    {
        Assert.True(EmailSourceFormat.IsEmail(EmailSourceFormat.RetainedMessageFileName("Instruction"), null));
        Assert.True(EmailSourceFormat.IsEmail(EmailSourceFormat.UntitledMessageFileName, null));
    }
}
