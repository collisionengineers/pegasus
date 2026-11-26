using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The conversation the e-mail dialogue stacks: the kept messages oldest
/// first, each message quoted in them that Pegasus does not keep once, in the
/// place it has in the e-mail.
/// </summary>
public sealed class RetainedMailConversationPolicyTests
{
    private const string Jane = "jane@example.com";
    private const string Desk = "desk@collisionengineers.co.uk";
    private const string Staff = "alex@collisionengineers.co.uk";

    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void KeptMessagesAreOldestFirst()
    {
        var later = Kept(60, Jane, "The second message of the two.");
        var earlier = Kept(0, Jane, "The first message of the two.");

        var stack = RetainedMailConversationPolicy.Build([later, earlier]);

        Assert.Equal<Guid?>([earlier.Id, later.Id], stack.Select(row => row.Id));
        Assert.Equal("The first message of the two.", stack[0].Text);
        Assert.Equal((DateTimeOffset?)earlier.ReceivedAtUtc, stack[0].ReceivedAtUtc);
        Assert.Null(stack[0].ForwardedByAddress);
    }

    [Fact]
    public void AQuotedMessagePegasusKeepsIsNotRepeated()
    {
        var instruction = Kept(0, Jane, "Please inspect AB12CDE on Thursday.");
        var reply = Kept(
            60,
            Desk,
            "Booked for Thursday.\n\n" + Block("Jane Doe", Jane, "02 November 2026 09:00", "Please inspect AB12CDE on Thursday."),
            RetainedMailDirection.Sent);

        var stack = RetainedMailConversationPolicy.Build([instruction, reply]);

        Assert.Equal<Guid?>([instruction.Id, reply.Id], stack.Select(row => row.Id));
        Assert.Equal("Booked for Thursday.", stack[1].Text);
        Assert.Equal(RetainedMailDirection.Sent, stack[1].Direction);
    }

    [Fact]
    public void AQuotedOnlyMessageFollowsItsAnchor()
    {
        var instruction = Kept(0, Jane, "Please inspect AB12CDE.");
        var answer = Kept(
            120,
            Jane,
            "Thursday is fine.\n\n"
                + Block("Desk", Desk, "02 November 2026 16:00", "Which day suits you?") + "\n\n"
                + Block("Jane Doe", Jane, "02 November 2026 09:00", "Please inspect AB12CDE."));

        var stack = RetainedMailConversationPolicy.Build([instruction, answer]);

        Assert.Collection(
            stack,
            first => Assert.Equal(instruction.Id, first.Id),
            quoted =>
            {
                Assert.Null(quoted.Id);
                Assert.Equal(RetainedMailDirection.Quoted, quoted.Direction);
                Assert.Equal(Desk, quoted.SenderAddress);
                Assert.Equal("Desk", quoted.SenderDisplayName);
                Assert.Equal("02 November 2026 16:00", quoted.QuotedSentText);
                Assert.Equal((DateTime?)new DateTime(2026, 11, 2, 16, 0, 0, DateTimeKind.Unspecified), quoted.QuotedSentLocal);
                Assert.Null(quoted.ReceivedAtUtc);
                Assert.Empty(quoted.Attachments);
                Assert.Null(quoted.IntakeReceiptId);
                Assert.Equal("Which day suits you?", quoted.Text);
            },
            last => Assert.Equal(answer.Id, last.Id));
    }

    [Fact]
    public void QuotedOnlyMessagesBeforeTheFirstAnchorGoBeforeIt()
    {
        var question = Kept(60, Desk, "Which day suits you?", RetainedMailDirection.Sent, name: "Desk");
        var answer = Kept(
            120,
            Jane,
            "Thursday.\n\n"
                + Block("Desk", Desk, "02 November 2026 10:00", "Which day suits you?") + "\n\n"
                + Block("Jane Doe", Jane, "02 November 2026 09:00", "Please inspect AB12CDE."));

        var stack = RetainedMailConversationPolicy.Build([question, answer]);

        Assert.Collection(
            stack,
            quoted =>
            {
                Assert.Null(quoted.Id);
                Assert.Equal("Please inspect AB12CDE.", quoted.Text);
            },
            first => Assert.Equal(question.Id, first.Id),
            last => Assert.Equal(answer.Id, last.Id));
    }

    [Fact]
    public void QuotedOnlyMessagesWithNoAnchorGoBeforeTheirCarrier()
    {
        var earlier = Kept(0, "bob@example.com", "An earlier note about the claim.");
        var answer = Kept(
            60,
            Jane,
            "Thursday.\n\n" + Block("Desk", Desk, "02 November 2026 10:00", "Which day suits you?"));

        var stack = RetainedMailConversationPolicy.Build([earlier, answer]);

        Assert.Equal(3, stack.Count);
        Assert.Equal(earlier.Id, stack[0].Id);
        Assert.Null(stack[1].Id);
        Assert.Equal("Which day suits you?", stack[1].Text);
        Assert.Equal(answer.Id, stack[2].Id);
    }

    [Fact]
    public void AQuotedOnlyMessageCarriedTwiceAppearsOnce()
    {
        var question = Block("Desk", Desk, "02 November 2026 10:00", "Which day suits you?");
        var first = Kept(60, Jane, "Thursday works for us.\n\n" + question);
        var second = Kept(
            120,
            Jane,
            "Friday works as well.\n\n"
                + Block("Jane Doe", Jane, "02 November 2026 11:00", "Thursday works for us.") + "\n\n"
                + question);

        var stack = RetainedMailConversationPolicy.Build([first, second]);

        Assert.Collection(
            stack,
            quoted =>
            {
                Assert.Null(quoted.Id);
                Assert.Equal("Which day suits you?", quoted.Text);
            },
            row => Assert.Equal(first.Id, row.Id),
            row => Assert.Equal(second.Id, row.Id));
    }

    [Fact]
    public void AStaffForwardOfAMessageIsThatMessageForwardedByStaff()
    {
        var forward = Kept(
            0,
            Staff,
            Block("Jane Doe", Jane, "02 November 2026 09:00", "Please inspect AB12CDE.") + "\n\n"
                + Block("Desk", Desk, "01 November 2026 16:00", "Send us the details."),
            effectiveSender: Jane);

        var stack = RetainedMailConversationPolicy.Build([forward]);

        Assert.Collection(
            stack,
            quoted =>
            {
                Assert.Null(quoted.Id);
                Assert.Equal("Send us the details.", quoted.Text);
            },
            row =>
            {
                Assert.Equal(forward.Id, row.Id);
                Assert.Equal("Jane Doe", row.SenderDisplayName);
                Assert.Equal(Jane, row.SenderAddress);
                Assert.Equal(Staff, row.ForwardedByAddress);
                Assert.Equal(forward.ToAddresses, row.ToAddresses);
                Assert.Equal("Please inspect AB12CDE.", row.Text);
            });
    }

    [Fact]
    public void AStaffForwardWithWordsOfItsOwnKeepsThemUnderTheOriginalSender()
    {
        var forward = Kept(0, Staff, "Please look at this one.", effectiveSender: Jane, name: "Alex");

        var row = Assert.Single(RetainedMailConversationPolicy.Build([forward]));

        Assert.Null(row.SenderDisplayName);
        Assert.Equal(Jane, row.SenderAddress);
        Assert.Equal(Staff, row.ForwardedByAddress);
        Assert.Equal("Please look at this one.", row.Text);
    }

    [Fact]
    public void NoKeptMessagesIsAnEmptyConversation() =>
        Assert.Empty(RetainedMailConversationPolicy.Build([]));

    private static KeptConversationMessage Kept(
        int minutes,
        string senderAddress,
        string body,
        RetainedMailDirection direction = RetainedMailDirection.Received,
        string? effectiveSender = null,
        string? name = null) => new(
        Guid.NewGuid(),
        direction,
        name,
        senderAddress,
        effectiveSender,
        [Desk],
        [],
        Start.AddMinutes(minutes),
        body,
        [],
        null);

    private static string Block(string name, string address, string sent, string text) =>
        $"From: {name} <{address}>\n" +
        $"Sent: {sent}\n" +
        "To: someone@example.com\n" +
        "Subject: Inspection\n\n" +
        text;
}
