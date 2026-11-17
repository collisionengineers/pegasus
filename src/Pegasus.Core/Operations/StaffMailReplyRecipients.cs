using System.Net.Mail;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

/// <summary>
/// Who a reply to one retained message goes to, for the message page, the
/// Triage Case's reply and the Automation Actor's <c>pegasus_mail_send</c>
/// alike.
/// </summary>
public static class StaffMailReplyRecipients
{
    private static readonly char[] Separators = [',', ';', '\n', '\r'];

    /// <summary>
    /// Reply answers the message's Reply-To, or From without one; a Collision
    /// Engineers staff forward held in instructions@ is answered as though the
    /// original had arrived directly, to its original sender (operator, 3
    /// November 2026; R651, R652).
    /// </summary>
    public static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) Reply(RetainedMailDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return (OriginalSender(detail.Summary) is { } original
            ? Parse(original)
            : Parse(detail.ReplyToAddresses), []);
    }

    /// <summary>
    /// Reply all adds the message's To and Cc, less <paramref name="ownAddress"/>;
    /// a staff forward held in instructions@ adds the original message's own,
    /// read from its forwarded header, less the mailbox that forwarded it too
    /// (operator, 3 November 2026; R652, R653).
    /// </summary>
    public static (StaffMailRecipient[] To, StaffMailRecipient[] Cc) ReplyAll(
        RetainedMailDetail detail,
        string? ownAddress)
    {
        ArgumentNullException.ThrowIfNull(detail);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (ownAddress is not null)
        {
            seen.Add(ownAddress);
        }
        var to = new List<StaffMailRecipient>();
        var cc = new List<StaffMailRecipient>();
        AddUnique(to, Reply(detail).To, seen);
        if (OriginalSender(detail.Summary) is null)
        {
            AddUnique(to, detail.ToAddresses.Select(AddressRecipient), seen);
            AddUnique(cc, detail.CcAddresses.Select(AddressRecipient), seen);
        }
        else
        {
            seen.Add(detail.Summary.SenderAddress!.Trim());
            var original = StaffForwardBodyCleaner.ForwardedRecipients(detail.BodyPlainText ?? string.Empty);
            AddUnique(to, original.To.Select(AddressRecipient), seen);
            AddUnique(cc, original.Cc.Select(AddressRecipient), seen);
        }
        return ([.. to], [.. cc]);
    }

    /// <summary>
    /// The Triage Case's reply answers the message's Reply-To, or From without
    /// one; a Collision Engineers staff forward, whichever mailbox holds it, is
    /// answered to its proven original sender instead of the forwarding desk
    /// (FRD-21 Reply targets, issue 1042).
    /// </summary>
    public static StaffMailRecipient[] TriageReply(RetainedMailDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        return PrincipalMailRoutePolicy.StaffForwardOriginalSender(
            detail.Summary.SenderAddress, detail.Summary.EffectiveSenderAddress) is { } originalSender
            ? ParseValid(originalSender)
            : Reply(detail).To
                .Where(item => IsMailboxAddress(item.Address))
                .ToArray();
    }

    /// <summary>
    /// The addresses in <paramref name="value"/>, split on commas, semicolons
    /// and line breaks, once each; none is checked to be a mailbox address.
    /// </summary>
    public static StaffMailRecipient[] Parse(string? value) =>
        (value ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AddressRecipient)
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>The mailbox addresses in <paramref name="values"/>, once each; any other entry is dropped.</summary>
    public static StaffMailRecipient[] Parse(IReadOnlyList<string>? values) =>
        (values ?? [])
            .SelectMany(Parse)
            .Where(item => IsMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// The mailbox addresses in <paramref name="value"/>, split as
    /// <see cref="Parse(string)"/> splits, once each; any other entry is
    /// dropped. The Triage Case's reply reads its To and Cc by it.
    /// </summary>
    public static StaffMailRecipient[] ParseValid(string? value) =>
        (value ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AddressRecipient)
            .Where(item => IsMailboxAddress(item.Address))
            .DistinctBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>Whether <paramref name="value"/> is one plain mailbox address and nothing else.</summary>
    public static bool IsMailboxAddress(string value) =>
        MailAddress.TryCreate(value, out var parsed)
        && string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase);

    /// <summary>The original sender a staff forward held in instructions@ is answered to; null otherwise.</summary>
    private static string? OriginalSender(RetainedMailSummary summary) =>
        StaffMailAnswer.OriginalSender(summary.MailboxAddress, summary.SenderAddress, summary.EffectiveSenderAddress);

    private static void AddUnique(
        List<StaffMailRecipient> target,
        IEnumerable<StaffMailRecipient> candidates,
        HashSet<string> seen)
    {
        foreach (var candidate in candidates)
        {
            if (seen.Add(candidate.Address))
            {
                target.Add(candidate);
            }
        }
    }

    private static StaffMailRecipient AddressRecipient(string address) =>
        new(address.Trim(), DisplayName: null);
}
