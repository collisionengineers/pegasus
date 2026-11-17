using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

public sealed class StaffMailReplyRecipientsTests
{
    private const string Instructions = "instructions@collisionengineers.co.uk";
    private const string Desk = "desk@collisionengineers.co.uk";
    private const string Handler = "handler@principal.example";

    private static readonly string[] DeskOnly = [Desk];
    private static readonly string[] HandlerOnly = [Handler];
    private static readonly string[] InstructionsOnly = [Instructions];
    private static readonly string[] CopyOnly = ["copy@principal.example"];

    private static readonly string[] ReplyToWithNoise =
        ["handler@principal.example; not an address", "HANDLER@principal.example", "second@principal.example"];

    private static readonly string[] ValidReplyTo = [Handler, "second@principal.example"];

    private static readonly string[] OrdinaryTo = [Desk, "Other@principal.example"];
    private static readonly string[] OrdinaryCc = ["other@principal.example", "HANDLER@principal.example", "copy@principal.example"];
    private static readonly string[] OrdinaryReplyAllTo = [Handler, "Other@principal.example"];

    private static readonly string[] ForwardedReplyAllTo = [Handler, "colleague@principal.example"];

    private static readonly string[] SplitUnchecked = ["a@x.example", "not an address", "b@x.example"];
    private static readonly string[] SplitValid = ["a@x.example", "b@x.example"];

    private const string ForwardBody =
        "Please see below.\n\n"
        + "From: Handler <handler@principal.example>\n"
        + "Sent: 3 November 2026 09:00\n"
        + "To: desk@collisionengineers.co.uk; colleague@principal.example\n"
        + "Cc: copy@principal.example\n"
        + "Subject: New instruction\n\n"
        + "Instruction body";

    [Fact]
    public void ReplyToAnOrdinaryMessageAnswersItsReplyToMailboxAddressesOnce()
    {
        var (to, cc) = StaffMailReplyRecipients.Reply(Message(Desk, Handler, replyTo: ReplyToWithNoise));

        Assert.Equal(ValidReplyTo, Addresses(to));
        Assert.Empty(cc);
    }

    /// <summary>
    /// A staff forward held in instructions@ is answered to its original
    /// sender (R651, R652); the same forward held in another mailbox is
    /// answered as itself.
    /// </summary>
    [Fact]
    public void ReplyToAStaffForwardHeldInInstructionsAnswersTheOriginalSender()
    {
        var held = StaffMailReplyRecipients.Reply(Message(Instructions, Desk, Handler, replyTo: DeskOnly));
        var elsewhere = StaffMailReplyRecipients.Reply(Message(Desk, Desk, Handler, replyTo: DeskOnly));

        Assert.Equal(HandlerOnly, Addresses(held.To));
        Assert.Equal(DeskOnly, Addresses(elsewhere.To));
    }

    [Fact]
    public void ReplyAllLeavesOutTheOwnAddressAndEachRepeat()
    {
        var detail = Message(Desk, Handler, replyTo: HandlerOnly, to: OrdinaryTo, cc: OrdinaryCc);

        var (to, cc) = StaffMailReplyRecipients.ReplyAll(detail, "DESK@collisionengineers.co.uk");

        Assert.Equal(OrdinaryReplyAllTo, Addresses(to));
        Assert.Equal(CopyOnly, Addresses(cc));
    }

    /// <summary>
    /// Reply all to a staff forward held in instructions@ adds the original
    /// message's To and Cc read from the forwarded header, less the mailbox
    /// that forwarded it (R652, R653); the forward's own To is not read.
    /// </summary>
    [Fact]
    public void ReplyAllToAStaffForwardReadsTheForwardedToAndCc()
    {
        var detail = Message(Instructions, Desk, Handler, replyTo: DeskOnly, to: InstructionsOnly, body: ForwardBody);

        var (to, cc) = StaffMailReplyRecipients.ReplyAll(detail, Instructions);

        Assert.Equal(ForwardedReplyAllTo, Addresses(to));
        Assert.Equal(CopyOnly, Addresses(cc));
    }

    /// <summary>
    /// The Triage Case's reply answers a staff forward's original sender
    /// whichever mailbox holds it, where the message page's Reply answers it
    /// only in instructions@.
    /// </summary>
    [Fact]
    public void TheTriageReplyAnswersAStaffForwardsOriginalSenderInAnyMailbox()
    {
        var detail = Message(Desk, Desk, Handler, replyTo: DeskOnly);

        Assert.Equal(HandlerOnly, Addresses(StaffMailReplyRecipients.TriageReply(detail)));
        Assert.Equal(DeskOnly, Addresses(StaffMailReplyRecipients.Reply(detail).To));
        Assert.Equal(
            ValidReplyTo,
            Addresses(StaffMailReplyRecipients.TriageReply(Message(Desk, Handler, replyTo: ReplyToWithNoise))));
    }

    [Fact]
    public void TheParserSplitsOnCommasSemicolonsAndLineBreaksWithoutCheckingAddresses()
    {
        const string value = "a@x.example, not an address;\nA@X.example\r\nb@x.example";

        Assert.Equal(SplitUnchecked, Addresses(StaffMailReplyRecipients.Parse(value)));
        Assert.Equal(SplitValid, Addresses(StaffMailReplyRecipients.ParseValid(value)));
        Assert.Empty(StaffMailReplyRecipients.Parse((string?)null));
    }

    [Theory]
    [InlineData("handler@principal.example", true)]
    [InlineData("Handler <handler@principal.example>", false)]
    [InlineData("not an address", false)]
    public void AMailboxAddressIsOnePlainAddress(string value, bool expected) =>
        Assert.Equal(expected, StaffMailReplyRecipients.IsMailboxAddress(value));

    private static string[] Addresses(IEnumerable<StaffMailRecipient> recipients) =>
        [.. recipients.Select(recipient => recipient.Address)];

    private static RetainedMailDetail Message(
        string mailbox,
        string sender,
        string? effectiveSender = null,
        IReadOnlyList<string>? replyTo = null,
        IReadOnlyList<string>? to = null,
        IReadOnlyList<string>? cc = null,
        string? body = null) =>
        new(
            new RetainedMailSummary(
                Guid.NewGuid(), Guid.NewGuid(), mailbox, sender, null, effectiveSender, "Subject", null,
                DateTimeOffset.UnixEpoch, false, 0, null, null, null, null),
            to ?? [],
            cc ?? [],
            replyTo,
            body,
            [],
            [],
            MailFolderScope.Inbox,
            null,
            null,
            "immutable-message",
            null,
            null);
}
