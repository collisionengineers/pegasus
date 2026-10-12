using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

/// <summary>
/// The retained email a Case was instructed by: the mailbox message its
/// origin receipt came from, with what a reply to it needs.
/// </summary>
public sealed record CaseInstructionEmail(
    Guid RetainedMessageId,
    Guid ApprovedMailboxId,
    string ImmutableMessageId,
    string? InternetMessageId,
    string? ConversationId,
    string? Subject,
    string? SenderAddress,
    string? EffectiveSenderAddress)
{
    public StaffMailOriginalMessage Original => new(
        RetainedMessageId, ApprovedMailboxId, ImmutableMessageId, InternetMessageId, ConversationId);

    public string ReplySubject => StaffMailReplySubject.For(Subject, SenderAddress, EffectiveSenderAddress);
}

public interface ICaseInstructionEmailQueries
{
    /// <summary>The Case's instruction email; null when the Case did not arrive by an approved mailbox.</summary>
    Task<CaseInstructionEmail?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// Who one Case send leaves as. With <see cref="ReplyTo"/> it is a reply to the
/// Case's instruction email from the mailbox that email arrived in; without, a
/// new message from the Outgoing mailbox staff chose.
/// </summary>
public sealed record CaseMailSender(ApprovedMailbox Mailbox, CaseInstructionEmail? ReplyTo)
{
    public StaffMailComposeMode Mode => ReplyTo is null ? StaffMailComposeMode.New : StaffMailComposeMode.Reply;
}

/// <summary>
/// The one rule for where a Case's report and chaser leave from (operator,
/// 12 October 2026): a reply to the instruction email, from the mailbox it
/// arrived in, while that mailbox can send; otherwise staff pick an Outgoing
/// mailbox and the message is new.
/// </summary>
public sealed class ResolveCaseMailSender(
    ICaseInstructionEmailQueries instructions,
    IApprovedMailboxStore mailboxes)
{
    /// <summary>The instruction reply when there is one to make, and the mailboxes staff may otherwise pick.</summary>
    public async Task<CaseMailSenderOptions> OptionsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var sending = (await mailboxes.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(mailbox => mailbox.CanSend)
            .ToArray();
        var instruction = await instructions.GetAsync(caseId, cancellationToken).ConfigureAwait(false);
        var replyMailbox = instruction is null ? null : sending.Sender(instruction.ApprovedMailboxId);
        return new(
            replyMailbox is null ? null : new CaseMailSender(replyMailbox, instruction),
            sending);
    }

    /// <summary>
    /// The sender for one send: the instruction reply when there is one,
    /// else <paramref name="fromMailboxId"/>, which must be able to send.
    /// </summary>
    public async Task<CaseMailSender> ExecuteAsync(
        Guid caseId, Guid? fromMailboxId, CancellationToken cancellationToken)
    {
        var options = await OptionsAsync(caseId, cancellationToken).ConfigureAwait(false);
        if (options.Reply is { } reply)
        {
            return reply;
        }

        var chosen = options.Mailboxes.SingleOrDefault(mailbox => mailbox.Id == fromMailboxId)
            ?? throw new CaseMailSenderRequiredException();
        return new(chosen, ReplyTo: null);
    }
}

/// <summary>The Case has no instruction email to reply to and no sending mailbox was chosen.</summary>
public sealed class CaseMailSenderRequiredException()
    : InvalidOperationException("A From mailbox is required.");

public sealed record CaseMailSenderOptions(
    CaseMailSender? Reply,
    IReadOnlyList<ApprovedMailbox> Mailboxes);

/// <summary>
/// A reply's subject: the original's with one "Re:"; a Collision Engineers
/// staff forward's own "FW:" is dropped first, since the reply answers the
/// forwarded sender.
/// </summary>
public static class StaffMailReplySubject
{
    public static string For(string? subject, string? senderAddress, string? effectiveSenderAddress)
    {
        var value = PrincipalMailRoutePolicy.StaffForwardOriginalSender(senderAddress, effectiveSenderAddress) is null
            ? subject?.Trim() ?? string.Empty
            : StaffForwardBodyCleaner.WithoutForwardPrefixes(subject);
        return value.StartsWith("Re:", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"Re: {value}".TrimEnd();
    }
}
