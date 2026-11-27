using System.Text;

namespace Pegasus.Core.Intake;

/// <summary>One message of a conversation that Pegasus keeps, as the conversation policy reads it.</summary>
/// <param name="EffectiveSenderAddress">The route's effective sender; for a staff forward, the original sender.</param>
/// <param name="Body">The cleaned whole body.</param>
/// <param name="HasHtmlBody">Whether the kept original has an HTML part; null when not known.</param>
/// <param name="SourceSha256">The kept original's content hash, as stored.</param>
public sealed record KeptConversationMessage(
    Guid Id,
    RetainedMailDirection Direction,
    string? SenderDisplayName,
    string? SenderAddress,
    string? EffectiveSenderAddress,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    DateTimeOffset ReceivedAtUtc,
    string Body,
    IReadOnlyList<RetainedMailAttachment> Attachments,
    Guid? IntakeReceiptId,
    bool? HasHtmlBody = null,
    string? SourceSha256 = null);

/// <summary>
/// The rule that turns the kept messages of a conversation into the stack the
/// e-mail dialogue shows, oldest first: each kept message once with its
/// sender's own words, and each message quoted inside them that Pegasus does
/// not keep, once, in the place it has in the e-mail.
/// </summary>
public static class RetainedMailConversationPolicy
{
    /// <summary>How much of each text's opening is compared.</summary>
    private const int OpeningLength = 80;

    /// <summary>The shortest opening that matches a longer one it begins.</summary>
    private const int MinimumPrefixLength = 12;

    public static IReadOnlyList<RetainedMailConversationMessage> Build(IReadOnlyList<KeptConversationMessage> kept)
    {
        if (kept is null || kept.Count == 0)
        {
            return [];
        }

        var carriers = kept
            .Where(message => message is not null)
            .OrderBy(message => message.ReceivedAtUtc)
            .ThenBy(message => message.Id)
            .Select(Carrier)
            .ToArray();
        var stack = new List<RetainedMailConversationMessage>(carriers.Select(carrier => carrier.Row));
        var openings = new Openings();
        foreach (var (row, chain) in carriers)
        {
            Place(stack, row, chain, openings);
        }

        return stack;
    }

    /// <summary>
    /// Walks one kept message's quoted messages from its oldest to its newest
    /// and places each one Pegasus does not keep and has not placed already.
    /// </summary>
    private static void Place(
        List<RetainedMailConversationMessage> stack,
        RetainedMailConversationMessage carrier,
        IReadOnlyList<QuotedMailMessage> chain,
        Openings openings)
    {
        var held = new List<RetainedMailConversationMessage>();
        int? lastPlaced = null;
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            var quoted = Quoted(chain[index]);
            var anchor = stack.FindIndex(row => SameMessage(row, quoted, openings));
            if (anchor >= 0)
            {
                if (held.Count > 0)
                {
                    stack.InsertRange(anchor, held);
                    anchor += held.Count;
                    held.Clear();
                }

                lastPlaced = anchor;
            }
            else if (held.Exists(row => SameMessage(row, quoted, openings)))
            {
                continue;
            }
            else if (lastPlaced is { } after)
            {
                stack.Insert(after + 1, quoted);
                lastPlaced = after + 1;
            }
            else
            {
                held.Add(quoted);
            }
        }

        if (held.Count > 0)
        {
            var carrierIndex = stack.FindIndex(row => ReferenceEquals(row, carrier));
            stack.InsertRange(Math.Max(carrierIndex, 0), held);
        }
    }

    /// <summary>A kept message's row, and the messages quoted beneath it that remain to place.</summary>
    private static (RetainedMailConversationMessage Row, IReadOnlyList<QuotedMailMessage> Chain) Carrier(
        KeptConversationMessage message)
    {
        var split = QuotedMailSplitter.Split(message.Body ?? string.Empty);
        var isStaffForward = !string.IsNullOrWhiteSpace(message.EffectiveSenderAddress)
            && !string.Equals(message.EffectiveSenderAddress, message.SenderAddress, StringComparison.OrdinalIgnoreCase);
        if (isStaffForward && split.Quoted.Count > 0 && string.IsNullOrWhiteSpace(split.OwnText))
        {
            // The forward carries nothing of its own: the row is the message
            // it forwards, and what that message quotes is the chain.
            var forwarded = split.Quoted[0];
            return (
                Kept(message, forwarded.SenderName, message.EffectiveSenderAddress, message.SenderAddress, forwarded.Text),
                split.Quoted.Skip(1).ToArray());
        }

        return isStaffForward
            ? (Kept(message, null, message.EffectiveSenderAddress, message.SenderAddress, split.OwnText), split.Quoted)
            : (Kept(message, message.SenderDisplayName, message.SenderAddress, null, split.OwnText), split.Quoted);
    }

    private static RetainedMailConversationMessage Kept(
        KeptConversationMessage message,
        string? senderDisplayName,
        string? senderAddress,
        string? forwardedByAddress,
        string text)
    {
        var offersAsSent = OffersAsSent(message);
        return new(
            message.Id,
            message.Direction,
            senderDisplayName,
            senderAddress,
            forwardedByAddress,
            message.ToAddresses ?? [],
            message.CcAddresses ?? [],
            message.ReceivedAtUtc,
            null,
            null,
            text,
            message.Attachments ?? [],
            message.IntakeReceiptId,
            offersAsSent,
            offersAsSent ? message.SourceSha256 : null);
    }

    /// <summary>
    /// Whether a kept message offers its view as sent: a received one whose
    /// original is kept with its receipt, unless it is known to have no HTML
    /// part; a sent one only when it is known to have one (R1279).
    /// </summary>
    private static bool OffersAsSent(KeptConversationMessage message) => message.Direction switch
    {
        RetainedMailDirection.Received => message.IntakeReceiptId is not null && message.HasHtmlBody != false,
        RetainedMailDirection.Sent => message.HasHtmlBody == true,
        _ => false
    };

    private static RetainedMailConversationMessage Quoted(QuotedMailMessage message) => new(
        null,
        RetainedMailDirection.Quoted,
        message.SenderName,
        message.SenderAddress,
        null,
        message.ToAddresses,
        message.CcAddresses,
        null,
        message.SentLocal,
        message.SentText,
        message.Text,
        [],
        null);

    /// <summary>
    /// Whether two rows are the same message: the same sender (address, when
    /// both have one, else display name) and the same opening text.
    /// </summary>
    private static bool SameMessage(
        RetainedMailConversationMessage left,
        RetainedMailConversationMessage right,
        Openings openings)
    {
        var sameSender = !string.IsNullOrWhiteSpace(left.SenderAddress) && !string.IsNullOrWhiteSpace(right.SenderAddress)
            ? string.Equals(left.SenderAddress.Trim(), right.SenderAddress.Trim(), StringComparison.OrdinalIgnoreCase)
            : !string.IsNullOrWhiteSpace(left.SenderDisplayName)
                && !string.IsNullOrWhiteSpace(right.SenderDisplayName)
                && string.Equals(left.SenderDisplayName.Trim(), right.SenderDisplayName.Trim(), StringComparison.OrdinalIgnoreCase);
        if (!sameSender)
        {
            return false;
        }

        var leftOpening = openings.Of(left);
        var rightOpening = openings.Of(right);
        if (string.Equals(leftOpening, rightOpening, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var (shorter, longer) = leftOpening.Length <= rightOpening.Length
            ? (leftOpening, rightOpening)
            : (rightOpening, leftOpening);
        return shorter.Length >= MinimumPrefixLength
            && longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Each row's <see cref="Opening"/>, made once: every quoted message is
    /// compared with every row of the stack, so a long conversation would
    /// otherwise rebuild the same openings for each comparison.
    /// </summary>
    private sealed class Openings
    {
        private readonly Dictionary<RetainedMailConversationMessage, string> made = new(ReferenceEqualityComparer.Instance);

        public string Of(RetainedMailConversationMessage row)
        {
            if (!made.TryGetValue(row, out var opening))
            {
                opening = Opening(row.Text);
                made[row] = opening;
            }

            return opening;
        }
    }

    /// <summary>The text with every whitespace run collapsed to one space, trimmed, cut at <see cref="OpeningLength"/>.</summary>
    private static string Opening(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var collapsed = new StringBuilder(Math.Min(text.Length, OpeningLength * 2));
        var pendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = collapsed.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                collapsed.Append(' ');
                pendingSpace = false;
            }

            collapsed.Append(character);
            if (collapsed.Length >= OpeningLength)
            {
                break;
            }
        }

        return collapsed.Length > OpeningLength
            ? collapsed.ToString(0, OpeningLength)
            : collapsed.ToString();
    }
}
