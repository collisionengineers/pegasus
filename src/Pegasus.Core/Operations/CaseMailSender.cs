using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

/// <summary>
/// The retained email a Case was instructed by: the mailbox message its
/// origin receipt came from, with what a reply to it needs.
/// <paramref name="ApprovedMailboxAddress"/> is the address of the mailbox
/// holding it, which decides whether a staff forward is answered as though
/// the original had arrived directly (<see cref="StaffMailAnswer"/>).
/// </summary>
public sealed record CaseInstructionEmail(
    Guid RetainedMessageId,
    Guid ApprovedMailboxId,
    string ApprovedMailboxAddress,
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

    /// <summary>The Case's Principal's Send from address; null when it has none.</summary>
    Task<string?> SendFromAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// Which mailbox a reply to one retained message leaves from (operator, 12
/// October and 3 November 2026). A message is answered from the mailbox it
/// arrived in. A Collision Engineers staff forward held in instructions@ is
/// answered as though it had arrived in the mailbox that forwarded it
/// ("Pretend instructions IS desk", R651; "Only instructions@", R652): from
/// that mailbox when it can send, otherwise from the mailbox staff choose. A
/// staff forward held in any other mailbox is answered as any message is. A
/// reply that leaves another mailbox than the one holding the message goes
/// as a new message, since only the holding mailbox has a copy to reply in.
/// </summary>
/// <param name="Fixed">The mailbox the reply leaves from, which staff cannot change.</param>
/// <param name="Choices">The mailboxes staff choose from when nothing is fixed; empty when the reply cannot be sent.</param>
public sealed record StaffMailAnswer(ApprovedMailbox? Fixed, IReadOnlyList<ApprovedMailbox> Choices)
{
    public bool CanSend => Fixed is not null || Choices.Count > 0;

    /// <summary>The fixed mailbox, else the choice <paramref name="chosenMailboxId"/> names; none when neither stands.</summary>
    public ApprovedMailbox? Sender(Guid? chosenMailboxId) =>
        Fixed ?? Choices.SingleOrDefault(mailbox => mailbox.Id == chosenMailboxId);

    /// <summary>
    /// The answer to the message held in <paramref name="holdingMailboxId"/>
    /// at <paramref name="holdingMailboxAddress"/>, sent by
    /// <paramref name="senderAddress"/>, among the mailboxes that can send.
    /// </summary>
    public static StaffMailAnswer For(
        Guid holdingMailboxId,
        string? holdingMailboxAddress,
        string? senderAddress,
        string? effectiveSenderAddress,
        IReadOnlyList<ApprovedMailbox> sending)
    {
        ArgumentNullException.ThrowIfNull(sending);
        if (OriginalSender(holdingMailboxAddress, senderAddress, effectiveSenderAddress) is null)
        {
            return new(sending.Sender(holdingMailboxId), []);
        }
        var forwarder = sending.SingleOrDefault(mailbox => mailbox.CanSend
            && string.Equals(mailbox.Address.Trim(), senderAddress!.Trim(), StringComparison.OrdinalIgnoreCase));
        return forwarder is null ? new(null, sending) : new(forwarder, []);
    }

    /// <summary>
    /// The original sender a message is answered to as though it had arrived
    /// directly: a Collision Engineers staff forward's
    /// (<see cref="PrincipalMailRoutePolicy.StaffForwardOriginalSender"/>)
    /// while instructions@ holds it (R651, R652). Null for every other
    /// message, a staff forward held in another mailbox included, which is
    /// answered as itself.
    /// </summary>
    public static string? OriginalSender(
        string? holdingMailboxAddress,
        string? senderAddress,
        string? effectiveSenderAddress) =>
        string.Equals(
            holdingMailboxAddress?.Trim(),
            PrincipalMailRoutePolicy.StaffForwardMailboxAddress,
            StringComparison.OrdinalIgnoreCase)
            ? PrincipalMailRoutePolicy.StaffForwardOriginalSender(senderAddress, effectiveSenderAddress)
            : null;
}

/// <summary>
/// Who one Case send leaves as. With <see cref="ReplyTo"/> it answers the
/// Case's instruction email: in its thread when <see cref="Mailbox"/> holds
/// it, otherwise as a new message (<see cref="StaffMailAnswer"/>). Without,
/// it is a new message from the Outgoing mailbox staff chose.
/// </summary>
public sealed record CaseMailSender(ApprovedMailbox Mailbox, CaseInstructionEmail? ReplyTo)
{
    public StaffMailComposeMode Mode => ReplyTo is null ? StaffMailComposeMode.New : StaffMailComposeMode.Reply;
}

/// <summary>
/// The one rule for where a Case's report and chaser leave from: the answer to
/// the Case's instruction email (<see cref="StaffMailAnswer"/>); with none,
/// or none that can send, staff pick an Outgoing mailbox, the Principal's
/// Send from chosen to start with.
/// </summary>
public sealed class ResolveCaseMailSender(
    ICaseInstructionEmailQueries instructions,
    IApprovedMailboxStore mailboxes)
{
    /// <summary>The fixed sender when there is one, and the mailboxes staff may otherwise pick.</summary>
    public async Task<CaseMailSenderOptions> OptionsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var sending = (await mailboxes.ListAsync(cancellationToken).ConfigureAwait(false))
            .Where(mailbox => mailbox.CanSend)
            .ToArray();
        var instruction = await instructions.GetAsync(caseId, cancellationToken).ConfigureAwait(false);
        var answer = instruction is null
            ? null
            : StaffMailAnswer.For(
                instruction.ApprovedMailboxId,
                instruction.ApprovedMailboxAddress,
                instruction.SenderAddress,
                instruction.EffectiveSenderAddress,
                sending);
        return new(
            answer?.Fixed is { } replyMailbox ? new CaseMailSender(replyMailbox, instruction) : null,
            sending,
            // A staff forward held in instructions@ is still answered from the mailbox staff choose.
            answer is { Choices.Count: > 0 } ? instruction : null);
    }

    /// <summary>
    /// The sender for one send: the fixed sender when there is one, else
    /// <paramref name="fromMailboxId"/>, which must be able to send.
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
        return options.Chosen(chosen);
    }
}

/// <summary>The Case has no instruction email to reply to and no sending mailbox was chosen.</summary>
public sealed class CaseMailSenderRequiredException()
    : InvalidOperationException("A From mailbox is required.");

/// <param name="Reply">The sender staff cannot change: the answer to the instruction email.</param>
/// <param name="Mailboxes">The Outgoing mailboxes staff pick from when there is no fixed sender.</param>
/// <param name="Answers">The staff-forward instruction a chosen mailbox still answers; null otherwise.</param>
public sealed record CaseMailSenderOptions(
    CaseMailSender? Reply,
    IReadOnlyList<ApprovedMailbox> Mailboxes,
    CaseInstructionEmail? Answers)
{
    /// <summary>The sender when staff chose <paramref name="mailbox"/>.</summary>
    public CaseMailSender Chosen(ApprovedMailbox mailbox) => new(mailbox, Answers);

    /// <summary>The subject an answer to the instruction email carries; null for a new message.</summary>
    public string? ReplySubject => (Reply?.ReplyTo ?? Answers)?.ReplySubject;
}

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
