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
