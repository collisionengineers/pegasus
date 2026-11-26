using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// An e-mail's text split into its own words and the messages quoted beneath
/// it, newest first, from the four header shapes the mail programs write.
/// </summary>
public sealed class QuotedMailSplitterTests
{
    private const string OutlookReply =
        "Thanks, booked in.\r\n\r\n" +
        "From: Nicholas Duncombe <nduncombe@qdosassist.co.uk>\r\n" +
        "Sent: 03 November 2026 10:15\r\n" +
        "To: desk@collisionengineers.co.uk\r\n" +
        "Cc: Claims Team <claims@qdosassist.co.uk>; ops@qdosassist.co.uk\r\n" +
        "Subject: Inspection AB12CDE\r\n\r\n" +
        "Please inspect the vehicle.\r\n";

    [Fact]
    public void OutlooksBlockStartsAQuotedMessageWithItsCcLine()
    {
        var split = QuotedMailSplitter.Split(OutlookReply);

        Assert.Equal("Thanks, booked in.", split.OwnText);
        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("Nicholas Duncombe", quoted.SenderName);
        Assert.Equal("nduncombe@qdosassist.co.uk", quoted.SenderAddress);
        Assert.Equal("03 November 2026 10:15", quoted.SentText);
        Assert.Equal(At(3, 10, 15), quoted.SentLocal);
        Assert.Equal(DateTimeKind.Unspecified, quoted.SentLocal?.Kind);
        Assert.Equal("Inspection AB12CDE", quoted.Subject);
        Assert.Equal(["desk@collisionengineers.co.uk"], quoted.ToAddresses);
        Assert.Equal(["claims@qdosassist.co.uk", "ops@qdosassist.co.uk"], quoted.CcAddresses);
        Assert.Equal("Please inspect the vehicle.", quoted.Text);
    }

    [Fact]
    public void AnOriginalMessageSeparatorStartsAQuotedMessage()
    {
        var split = QuotedMailSplitter.Split(
            "Please see below.\n\n" +
            "-----Original Message-----\n" +
            "From: jane@example.com\n" +
            "Sent: Tuesday, November 3, 2026 10:15 AM\n" +
            "To: desk@collisionengineers.co.uk\n" +
            "Subject: RE: Inspection\n\n" +
            "Can you confirm the date?");

        Assert.Equal("Please see below.", split.OwnText);
        var quoted = Assert.Single(split.Quoted);
        Assert.Null(quoted.SenderName);
        Assert.Equal("jane@example.com", quoted.SenderAddress);
        Assert.Equal(At(3, 10, 15), quoted.SentLocal);
        Assert.Equal("RE: Inspection", quoted.Subject);
        Assert.Equal("Can you confirm the date?", quoted.Text);
    }

    [Fact]
    public void AnOriginalMessageSeparatorReadsItsFieldsInAnyOrder()
    {
        var split = QuotedMailSplitter.Split(
            "Noted.\n\n" +
            "-----Original Message-----\n" +
            "Subject: Inspection\n" +
            "Date: Tue, 3 Nov 2026 10:15:00 +0000\n" +
            "From: \"Doe, Jane\" <jane@example.com>\n\n" +
            "Can you confirm the date?");

        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("Doe, Jane", quoted.SenderName);
        Assert.Equal("jane@example.com", quoted.SenderAddress);
        Assert.Equal("Tue, 3 Nov 2026 10:15:00 +0000", quoted.SentText);
        Assert.Equal(At(3, 10, 15), quoted.SentLocal);
        Assert.Empty(quoted.ToAddresses);
        Assert.Equal("Can you confirm the date?", quoted.Text);
    }

    [Fact]
    public void AWroteLineStartsAQuotedMessageAndLosesOneQuoteLevel()
    {
        var split = QuotedMailSplitter.Split(
            "Yes, Thursday works.\n\n" +
            "On Tue, 3 Nov 2026 at 10:15, Jane Doe <jane@example.com> wrote:\n" +
            "> Can you inspect on Thursday?\n" +
            ">\n" +
            "> Jane\n");

        Assert.Equal("Yes, Thursday works.", split.OwnText);
        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("Jane Doe", quoted.SenderName);
        Assert.Equal("jane@example.com", quoted.SenderAddress);
        Assert.Equal("Tue, 3 Nov 2026 at 10:15", quoted.SentText);
        Assert.Equal(At(3, 10, 15), quoted.SentLocal);
        Assert.Equal("Can you inspect on Thursday?\n\nJane", quoted.Text);
    }

    [Fact]
    public void AWroteLineWrappedOntoTwoLinesIsOneHeader()
    {
        var split = QuotedMailSplitter.Split(
            "Yes.\n\n" +
            "On Tue, 3 Nov 2026 at 10:15, Jane Doe <jane@example.com>\n" +
            "wrote:\n" +
            "> Thursday?\n");

        Assert.Equal("Yes.", split.OwnText);
        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("jane@example.com", quoted.SenderAddress);
        Assert.Equal("Thursday?", quoted.Text);
    }

    [Fact]
    public void AWroteLineWithANameAloneHasNoAddress()
    {
        var split = QuotedMailSplitter.Split(
            "Yes.\n\n" +
            "On 3 Nov 2026, at 10:15, Jane Doe wrote:\n" +
            "> Thursday?\n");

        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("Jane Doe", quoted.SenderName);
        Assert.Null(quoted.SenderAddress);
        Assert.Equal("3 Nov 2026, at 10:15", quoted.SentText);
        Assert.Equal(At(3, 10, 15), quoted.SentLocal);
    }

    [Fact]
    public void ADeeperWroteLineIsTheNextQuotedMessage()
    {
        var split = QuotedMailSplitter.Split(
            "Done.\n\n" +
            "On Wed, 4 Nov 2026 at 09:00, Bob <bob@example.com> wrote:\n" +
            "> Is it done?\n" +
            ">\n" +
            "> On Tue, 3 Nov 2026 at 10:15, Jane Doe <jane@example.com> wrote:\n" +
            ">> Please inspect.\n");

        Assert.Equal("Done.", split.OwnText);
        Assert.Collection(
            split.Quoted,
            newer =>
            {
                Assert.Equal("bob@example.com", newer.SenderAddress);
                Assert.Equal("Is it done?", newer.Text);
            },
            older =>
            {
                Assert.Equal("jane@example.com", older.SenderAddress);
                Assert.Equal(At(3, 10, 15), older.SentLocal);
                Assert.Equal("Please inspect.", older.Text);
            });
    }

    [Fact]
    public void DeeperQuotingWithoutItsOwnWroteLineStaysText()
    {
        var split = QuotedMailSplitter.Split(
            "Done.\n\n" +
            "On Wed, 4 Nov 2026 at 09:00, Bob <bob@example.com> wrote:\n" +
            "> Is it done?\n" +
            ">> Older words.\n");

        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("Is it done?\n> Older words.", quoted.Text);
    }

    [Fact]
    public void GmailsForwardedSeparatorStartsAQuotedMessage()
    {
        var split = QuotedMailSplitter.Split(
            "---------- Forwarded message ---------\n" +
            "From: Jane Doe <jane@example.com>\n" +
            "Date: Tue, 3 Nov 2026 at 10:15\n" +
            "Subject: Inspection AB12CDE\n" +
            "To: <desk@collisionengineers.co.uk>\n" +
            "Cc: Bob <bob@example.com>\n\n" +
            "Photos attached.");

        Assert.Equal(string.Empty, split.OwnText);
        var quoted = Assert.Single(split.Quoted);
        Assert.Equal("Jane Doe", quoted.SenderName);
        Assert.Equal("jane@example.com", quoted.SenderAddress);
        Assert.Equal(At(3, 10, 15), quoted.SentLocal);
        Assert.Equal("Inspection AB12CDE", quoted.Subject);
        Assert.Equal(["desk@collisionengineers.co.uk"], quoted.ToAddresses);
        Assert.Equal(["bob@example.com"], quoted.CcAddresses);
        Assert.Equal("Photos attached.", quoted.Text);
    }

    [Fact]
    public void AChainOfThreeComesBackNewestFirstEachWithItsOwnWords()
    {
        var split = QuotedMailSplitter.Split(
            "Booked for Thursday.\n\n" +
            "From: Jane Doe <jane@example.com>\n" +
            "Sent: 03 November 2026 10:15\n" +
            "To: desk@collisionengineers.co.uk\n" +
            "Subject: RE: Inspection\n\n" +
            "Thursday please.\n\n" +
            "From: Desk <desk@collisionengineers.co.uk>\n" +
            "Sent: 02 November 2026 16:00\n" +
            "To: jane@example.com\n" +
            "Subject: RE: Inspection\n\n" +
            "Which day suits?\n\n" +
            "-----Original Message-----\n" +
            "From: Jane Doe <jane@example.com>\n" +
            "Sent: 02 November 2026 09:00\n" +
            "To: desk@collisionengineers.co.uk\n" +
            "Subject: Inspection\n\n" +
            "Please inspect AB12CDE.");

        Assert.Equal("Booked for Thursday.", split.OwnText);
        Assert.Collection(
            split.Quoted,
            first =>
            {
                Assert.Equal("jane@example.com", first.SenderAddress);
                Assert.Equal("Thursday please.", first.Text);
            },
            second =>
            {
                Assert.Equal("Desk", second.SenderName);
                Assert.Equal(At(2, 16, 0), second.SentLocal);
                Assert.Equal("Which day suits?", second.Text);
            },
            third =>
            {
                Assert.Equal("Inspection", third.Subject);
                Assert.Equal(At(2, 9, 0), third.SentLocal);
                Assert.Equal("Please inspect AB12CDE.", third.Text);
            });
    }

    [Fact]
    public void ABodyWithNoHeaderIsAllItsOwnWords()
    {
        var split = QuotedMailSplitter.Split("  Just a note.\r\nOn Monday we inspected it.\r\n\r\n");

        Assert.Equal("Just a note.\nOn Monday we inspected it.", split.OwnText);
        Assert.Empty(split.Quoted);
        Assert.Equal(string.Empty, QuotedMailSplitter.Split(string.Empty).OwnText);
    }

    [Theory]
    [InlineData("figures")]
    [InlineData("words")]
    public void ADateItCannotReadKeepsItsTextAndHasNoTime(string key)
    {
        var sent = key == "figures" ? "03/11/2026 10:15" : "sometime last week";

        var quoted = Assert.Single(QuotedMailSplitter.Split(
            "Noted.\n\n" +
            "From: Jane Doe <jane@example.com>\n" +
            $"Sent: {sent}\n" +
            "To: desk@collisionengineers.co.uk\n" +
            "Subject: Inspection\n\n" +
            "Please inspect.").Quoted);

        Assert.Equal(sent, quoted.SentText);
        Assert.Null(quoted.SentLocal);
    }

    [Theory]
    [InlineData("angle")]
    [InlineData("mailto")]
    [InlineData("bare")]
    [InlineData("name")]
    [InlineData("quoted")]
    public void TheSenderIsReadFromEachFromForm(string key)
    {
        var from = key switch
        {
            "angle" => "Jane Doe <jane@example.com>",
            "mailto" => "Jane Doe <jane@example.com<mailto:jane@example.com>>",
            "bare" => "jane@example.com",
            "name" => "Jane Doe",
            _ => "\"Doe, Jane\" <jane@example.com>"
        };
        string? name = key switch
        {
            "bare" => null,
            "quoted" => "Doe, Jane",
            _ => "Jane Doe"
        };
        string? address = key == "name" ? null : "jane@example.com";

        var quoted = Assert.Single(QuotedMailSplitter.Split(
            "Noted.\n\n" +
            $"From: {from}\n" +
            "Sent: 03 November 2026 10:15\n" +
            "To: desk@collisionengineers.co.uk\n" +
            "Subject: Inspection\n\n" +
            "Please inspect.").Quoted);

        Assert.Equal(name, quoted.SenderName);
        Assert.Equal(address, quoted.SenderAddress);
    }

    private static DateTime? At(int day, int hour, int minute) =>
        new DateTime(2026, 11, day, hour, minute, 0, DateTimeKind.Unspecified);
}
