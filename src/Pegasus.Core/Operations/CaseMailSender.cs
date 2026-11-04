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
/// Which mailbox a reply to one retained message leaves from: any send-ready
/// mailbox staff choose, starting on a default ("The SOP/autopopulate should
/// be changeable not enforced", operator, 4 November 2026; R655). The default
/// is the mailbox the message arrived in (operator, 12 October 2026). A
/// Collision Engineers staff forward held in instructions@ defaults to the
/// mailbox that forwarded it ("Pretend instructions IS desk", R651; "Only
/// instructions@", R652); a staff forward held in any other mailbox defaults
/// as any message does. With no default that can send, the Principal's Send
/// from is the default (<see cref="OrSendFromAsync"/>). A reply that leaves
/// another mailbox than the one holding the message goes as a new message,
/// since only the holding mailbox has a copy to reply in.
/// </summary>
/// <param name="Default">The mailbox the reply leaves from unless staff choose another; null when none is.</param>
/// <param name="Choices">Every mailbox that can send, which staff choose from; empty when the reply cannot be sent.</param>
public sealed record StaffMailAnswer(ApprovedMailbox? Default, IReadOnlyList<ApprovedMailbox> Choices)
{
    /// <summary>No mailbox can answer.</summary>
    public static StaffMailAnswer None { get; } = new(null, []);

    public bool CanSend => Choices.Count > 0;

    /// <summary>
    /// The choice <paramref name="chosenMailboxId"/> names when staff chose
    /// one, which must be able to send; else the default. None when neither
    /// stands.
    /// </summary>
    public ApprovedMailbox? Sender(Guid? chosenMailboxId) => chosenMailboxId is { } chosen
        ? Choices.SingleOrDefault(mailbox => mailbox.Id == chosen)
        : Default;

    /// <summary>
    /// This answer with the Principal's Send from of <paramref name="caseId"/>'s
    /// Case as its default when it has none, read only then.
    /// </summary>
    public async Task<StaffMailAnswer> OrSendFromAsync(
        ICaseInstructionEmailQueries cases, Guid? caseId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cases);
        if (Default is not null || !CanSend || caseId is not { } id)
        {
            return this;
        }
        var sendFrom = await cases.SendFromAsync(id, cancellationToken).ConfigureAwait(false);
        return this with { Default = Named(Choices, sendFrom) };
    }

    /// <summary>
    /// The answer to the message held in <paramref name="holdingMailboxId"/>
    /// at <paramref name="holdingMailboxAddress"/>, sent by
    /// <paramref name="senderAddress"/>, among <paramref name="mailboxes"/>.
    /// </summary>
    public static StaffMailAnswer For(
        Guid holdingMailboxId,
        string? holdingMailboxAddress,
        string? senderAddress,
        string? effectiveSenderAddress,
        IReadOnlyList<ApprovedMailbox> mailboxes)
    {
        ArgumentNullException.ThrowIfNull(mailboxes);
        ApprovedMailbox[] sending = [.. mailboxes.Where(mailbox => mailbox.CanSend)];
        return new(
            OriginalSender(holdingMailboxAddress, senderAddress, effectiveSenderAddress) is null
                ? sending.Sender(holdingMailboxId)
                : Named(sending, senderAddress),
            sending);
    }

    /// <summary>The mailbox among <paramref name="sending"/> at <paramref name="address"/>; none otherwise.</summary>
    private static ApprovedMailbox? Named(IReadOnlyList<ApprovedMailbox> sending, string? address) =>
        string.IsNullOrWhiteSpace(address)
            ? null
            : sending.FirstOrDefault(mailbox =>
                string.Equals(mailbox.Address.Trim(), address.Trim(), StringComparison.OrdinalIgnoreCase));

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
/// it is a new message from the Outgoing mailbox it names.
/// </summary>
public sealed record CaseMailSender(ApprovedMailbox Mailbox, CaseInstructionEmail? ReplyTo)
{
    public StaffMailComposeMode Mode => ReplyTo is null ? StaffMailComposeMode.New : StaffMailComposeMode.Reply;
}

/// <summary>
/// The one rule for where a Case's report and chaser leave from: any
/// send-ready Outgoing mailbox staff choose (R655), starting on the default
/// answer to the Case's instruction email (<see cref="StaffMailAnswer"/>);
/// with none that can send, on the Principal's Send from. Whichever mailbox
/// sends, a Case with an instruction email answers it.
/// </summary>
public sealed class ResolveCaseMailSender(
    ICaseInstructionEmailQueries instructions,
    IApprovedMailboxStore mailboxes)
{
    /// <summary>The default sender, the mailboxes staff may choose instead, and the instruction email any of them answers.</summary>
    public async Task<CaseMailSenderOptions> OptionsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        var approved = await mailboxes.ListAsync(cancellationToken).ConfigureAwait(false);
        var instruction = await instructions.GetAsync(caseId, cancellationToken).ConfigureAwait(false);
        var answer = await (instruction is null
                ? new StaffMailAnswer(null, [.. approved.Where(mailbox => mailbox.CanSend)])
                : StaffMailAnswer.For(
                    instruction.ApprovedMailboxId,
                    instruction.ApprovedMailboxAddress,
                    instruction.SenderAddress,
                    instruction.EffectiveSenderAddress,
                    approved))
            .OrSendFromAsync(instructions, caseId, cancellationToken)
            .ConfigureAwait(false);
        return new(
            answer.Default is { } start ? new CaseMailSender(start, instruction) : null,
            answer.Choices,
            instruction);
    }

    /// <summary>
    /// The sender for one send: <paramref name="fromMailboxId"/> when staff
    /// chose it, which must be able to send; else the default.
    /// </summary>
    public async Task<CaseMailSender> ExecuteAsync(
        Guid caseId, Guid? fromMailboxId, CancellationToken cancellationToken)
    {
        var options = await OptionsAsync(caseId, cancellationToken).ConfigureAwait(false);
        return options.Sender(fromMailboxId) ?? throw new CaseMailSenderRequiredException();
    }
}

/// <summary>No sending mailbox was chosen and none is the default, or the one chosen cannot send.</summary>
public sealed class CaseMailSenderRequiredException()
    : InvalidOperationException("A From mailbox is required.");

/// <param name="Default">The sender unless staff choose another; null when no mailbox is the default.</param>
/// <param name="Mailboxes">The send-ready Outgoing mailboxes staff choose from.</param>
/// <param name="ReplyTo">The instruction email whichever mailbox sends answers; null for a new message.</param>
public sealed record CaseMailSenderOptions(
    CaseMailSender? Default,
    IReadOnlyList<ApprovedMailbox> Mailboxes,
    CaseInstructionEmail? ReplyTo)
{
    /// <summary>No mailbox can send.</summary>
    public static CaseMailSenderOptions None { get; } = new(null, [], null);

    /// <summary>
    /// The sender from the mailbox <paramref name="chosenMailboxId"/> names
    /// when staff chose one, which must be able to send; else the default.
    /// </summary>
    public CaseMailSender? Sender(Guid? chosenMailboxId)
    {
        if (chosenMailboxId is not { } chosen)
        {
            return Default;
        }
        var mailbox = Mailboxes.SingleOrDefault(candidate => candidate.Id == chosen);
        return mailbox is null ? null : new CaseMailSender(mailbox, ReplyTo);
    }

    /// <summary>The subject an answer to the instruction email carries; null for a new message.</summary>
    public string? ReplySubject => ReplyTo?.ReplySubject;
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
