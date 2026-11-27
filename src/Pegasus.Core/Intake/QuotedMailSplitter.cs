using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// One message quoted inside an e-mail's text, read from the header that
/// starts it.
/// </summary>
/// <param name="SentText">The header's own date text, exactly as written.</param>
/// <param name="SentLocal">
/// The same wall-clock time where it can be read, in no zone
/// (<see cref="DateTimeKind.Unspecified"/>); null when it cannot.
/// </param>
/// <param name="Text">
/// That message's own words, trimmed, without the messages quoted beneath it.
/// </param>
public sealed record QuotedMailMessage(
    string? SenderName,
    string? SenderAddress,
    string? SentText,
    DateTime? SentLocal,
    string? Subject,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    string Text);

/// <summary>
/// An e-mail's text split into what its sender wrote and the messages quoted
/// beneath it, in the order they appear in the e-mail (newest first).
/// </summary>
public sealed record SplitMailBody(string OwnText, IReadOnlyList<QuotedMailMessage> Quoted);

/// <summary>
/// Splits an e-mail's text into its own words and the messages quoted beneath
/// it. Four header shapes start a quoted message, which runs to the next
/// header or the end: Outlook's From/Sent/To/Subject block (the
/// <see cref="StaffForwardBodyCleaner.ForwardedHeaderPattern"/> the rest of
/// intake reads), Outlook's "-----Original Message-----" separator, an
/// "On … wrote:" line over "&gt;"-quoted text, and Gmail's forwarded-message
/// separator. A pure text policy that never throws; a date is read only from
/// the exact forms listed here and is otherwise left unread.
/// </summary>
public static partial class QuotedMailSplitter
{
    /// <summary>How deep "&gt;"-quoted messages are followed before the rest is left as text.</summary>
    private const int MaximumDepth = 32;

    /// <summary>The longest "On … wrote:" line, joined when wrapped, that is read as a header.</summary>
    private const int MaximumAttributionLength = 400;

    private static readonly CultureInfo[] DateCultures =
    [
        CultureInfo.InvariantCulture,
        CultureInfo.GetCultureInfo("en-GB")
    ];

    // The exact forms a quoted header's date is read in. A form that names
    // the day and month in figures only ("03/11/2026") is left out: its order
    // depends on the writer's region, and the date is never guessed.
    private static readonly string[] DateFormats =
    [
        "dd MMMM yyyy HH:mm",
        "d MMMM yyyy HH:mm",
        "d MMMM yyyy HH:mm:ss",
        "dddd, d MMMM yyyy HH:mm",
        "dddd, d MMMM yyyy HH:mm:ss",
        "dddd, d MMMM yyyy h:mm tt",
        "dddd, d MMMM yyyy 'at' HH:mm",
        "dddd, MMMM d, yyyy h:mm tt",
        "dddd, MMMM d, yyyy h:mm:ss tt",
        "dddd, MMMM d, yyyy HH:mm",
        "ddd, d MMM yyyy 'at' HH:mm",
        "ddd, d MMM yyyy 'at' h:mm tt",
        "ddd, MMM d, yyyy 'at' h:mm tt",
        "ddd, MMM d, yyyy h:mm tt",
        "d MMM yyyy, 'at' HH:mm",
        "d MMM yyyy 'at' HH:mm",
        "d MMM yyyy, HH:mm",
        "d MMM yyyy HH:mm",
        "d MMM yyyy HH:mm:ss",
        "d MMMM yyyy, HH:mm",
        "d MMMM yyyy 'at' HH:mm",
        "ddd, d MMM yyyy HH:mm:ss",
        "ddd, d MMM yyyy HH:mm",
        "ddd, dd MMM yyyy HH:mm:ss",
        "dddd, d MMMM yyyy, HH:mm"
    ];

    /// <summary>
    /// The text's own words and the messages quoted beneath it, newest first.
    /// A text with no quoted header is all its own words.
    /// </summary>
    public static SplitMailBody Split(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return new(string.Empty, []);
        }

        var text = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var quoted = new List<QuotedMailMessage>();
        var ownText = SplitInto(text, quoted, depth: 0);
        return new(ownText, quoted);
    }

    /// <summary>
    /// Adds the messages quoted in <paramref name="text"/> to
    /// <paramref name="quoted"/> in the order they appear, and returns the
    /// words above the first of them.
    /// </summary>
    private static string SplitInto(string text, List<QuotedMailMessage> quoted, int depth)
    {
        var lines = text.Split('\n');
        var headers = depth >= MaximumDepth ? [] : FindHeaders(text, lines);
        if (headers.Count == 0)
        {
            return text.Trim();
        }

        var ownText = string.Join('\n', lines, 0, headers[0].StartLine).Trim();
        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index];
            var end = index + 1 < headers.Count ? headers[index + 1].StartLine : lines.Length;
            var block = lines[header.EndLine..end];
            var nested = new List<QuotedMailMessage>();
            string messageText;
            if (header.QuotesBeneath)
            {
                // The text beneath "On … wrote:" carries one more "> " than the
                // message it quotes; a deeper message with its own "wrote:"
                // line is the next quoted message.
                messageText = SplitInto(string.Join('\n', WithoutOneQuoteLevel(block)), nested, depth + 1);
            }
            else
            {
                messageText = string.Join('\n', block).Trim();
            }

            var (senderName, senderAddress) = ReadSender(header.From);
            var sentText = Blank(header.Sent);
            quoted.Add(new QuotedMailMessage(
                senderName,
                senderAddress,
                sentText,
                sentText is not null && TryReadDate(sentText, out var sentLocal) ? sentLocal : (DateTime?)null,
                Blank(header.Subject),
                Addresses(header.To),
                Addresses(header.Cc),
                messageText));
            quoted.AddRange(nested);
        }

        return ownText;
    }

    /// <summary>Every quoted header at the top level of the text, in order and not overlapping.</summary>
    private static List<QuotedHeader> FindHeaders(string text, string[] lines)
    {
        var lineStarts = new int[lines.Length];
        for (int index = 1, offset = 0; index < lines.Length; index++)
        {
            offset += lines[index - 1].Length + 1;
            lineStarts[index] = offset;
        }

        var candidates = new List<QuotedHeader>();
        foreach (Match match in StaffForwardBodyCleaner.ForwardedHeaderPattern.Matches(text))
        {
            var fromAt = text[match.Index] == '\n' ? match.Index + 1 : match.Index;
            var startLine = LineOf(lineStarts, fromAt);
            var endLine = LineOf(lineStarts, match.Index + match.Length - 1) + 1;
            var fields = ReadFields(lines, startLine, endLine);
            candidates.Add(new QuotedHeader(
                startLine,
                endLine,
                QuotesBeneath: false,
                fields.From,
                fields.Sent,
                fields.To,
                fields.Cc,
                fields.Subject));
        }

        for (var index = 0; index < lines.Length; index++)
        {
            if (SeparatorRegex().IsMatch(lines[index]))
            {
                candidates.Add(SeparatedHeader(lines, index));
            }
            else if (AttributionHeader(lines, index) is { } attribution)
            {
                candidates.Add(attribution);
            }
        }

        var headers = new List<QuotedHeader>();
        foreach (var candidate in candidates.OrderBy(item => item.StartLine).ThenByDescending(item => item.EndLine))
        {
            if (headers.Count == 0 || candidate.StartLine >= headers[^1].EndLine)
            {
                headers.Add(candidate);
            }
        }

        return headers;
    }

    /// <summary>
    /// A "-----Original Message-----" or "---------- Forwarded message
    /// ---------" line and the field lines beneath it, in any order.
    /// </summary>
    private static QuotedHeader SeparatedHeader(string[] lines, int separatorLine)
    {
        var line = separatorLine + 1;
        while (line < lines.Length && string.IsNullOrWhiteSpace(lines[line]) && line - separatorLine <= 2)
        {
            line++;
        }

        var firstField = line;
        while (line < lines.Length && FieldLineRegex().IsMatch(lines[line]))
        {
            line++;
        }

        var endLine = line > firstField ? line : separatorLine + 1;
        var fields = ReadFields(lines, separatorLine + 1, endLine);
        return new QuotedHeader(
            separatorLine,
            endLine,
            QuotesBeneath: false,
            fields.From,
            fields.Sent,
            fields.To,
            fields.Cc,
            fields.Subject);
    }

    /// <summary>
    /// An "On &lt;date&gt;, &lt;name&gt; &lt;address&gt; wrote:" line, or the
    /// same line wrapped onto two by the mail program; null when the line is
    /// not one.
    /// </summary>
    private static QuotedHeader? AttributionHeader(string[] lines, int index)
    {
        var line = lines[index];
        if (line.Length > MaximumAttributionLength || !AttributionStartRegex().IsMatch(line))
        {
            return null;
        }

        var endLine = index + 1;
        var attribution = AttributionRegex().Match(line);
        if (!attribution.Success
            && index + 1 < lines.Length
            && lines[index + 1].Length <= MaximumAttributionLength
            && !AttributionStartRegex().IsMatch(lines[index + 1]))
        {
            attribution = AttributionRegex().Match(line.TrimEnd() + " " + lines[index + 1].Trim());
            endLine = index + 2;
        }

        if (!attribution.Success || attribution.Length > MaximumAttributionLength)
        {
            return null;
        }

        var (sent, from) = ReadAttribution(attribution.Groups["content"].Value);
        return new QuotedHeader(index, endLine, QuotesBeneath: true, from, sent, null, null, null);
    }

    /// <summary>
    /// The date and the sender of an attribution line's content. The date is
    /// the longest opening that reads as a date; when none does, it runs to
    /// the last comma, or is the whole content when there is none.
    /// </summary>
    private static (string? Date, string? Sender) ReadAttribution(string content)
    {
        var rest = content.Trim();
        var address = string.Empty;
        if (rest.EndsWith('>'))
        {
            var open = OpeningOfLastBracket(rest);
            if (open >= 0)
            {
                address = rest[open..];
                rest = rest[..open].TrimEnd();
            }
        }
        else
        {
            var lastSpace = rest.LastIndexOf(' ');
            var last = rest[(lastSpace + 1)..];
            if (last.Contains('@', StringComparison.Ordinal))
            {
                address = last;
                rest = lastSpace < 0 ? string.Empty : rest[..lastSpace].TrimEnd();
            }
        }

        var tokens = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string date;
        string name;
        var dated = -1;
        for (var count = tokens.Length; count >= 1; count--)
        {
            if (TryReadDate(string.Join(' ', tokens, 0, count).TrimEnd(','), out _))
            {
                dated = count;
                break;
            }
        }

        if (dated > 0)
        {
            date = string.Join(' ', tokens, 0, dated).TrimEnd(',');
            name = string.Join(' ', tokens, dated, tokens.Length - dated);
        }
        else
        {
            var comma = rest.LastIndexOf(',');
            date = comma >= 0 ? rest[..comma] : rest;
            name = comma >= 0 ? rest[(comma + 1)..] : string.Empty;
        }

        name = name.Trim().Trim(',').Trim();
        return (Blank(date), Blank($"{name} {address}"));
    }

    /// <summary>The index of the "&lt;" that opens the bracket group ending the text, or -1.</summary>
    private static int OpeningOfLastBracket(string text)
    {
        var depth = 0;
        for (var index = text.Length - 1; index >= 0; index--)
        {
            if (text[index] == '>')
            {
                depth++;
            }
            else if (text[index] == '<')
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static IEnumerable<string> WithoutOneQuoteLevel(string[] lines)
    {
        foreach (var line in lines)
        {
            var start = 0;
            while (start < line.Length && line[start] == ' ')
            {
                start++;
            }

            if (start < line.Length && line[start] == '>')
            {
                var after = start + 1;
                yield return after < line.Length && line[after] == ' ' ? line[(after + 1)..] : line[after..];
            }
            else
            {
                yield return line;
            }
        }
    }

    /// <summary>The header fields on lines [<paramref name="startLine"/>, <paramref name="endLine"/>).</summary>
    private static HeaderFields ReadFields(string[] lines, int startLine, int endLine)
    {
        string? from = null;
        string? sent = null;
        string? to = null;
        string? subject = null;
        var copied = new List<string>();
        for (var index = startLine; index < endLine && index < lines.Length; index++)
        {
            var field = FieldLineRegex().Match(lines[index]);
            if (!field.Success)
            {
                continue;
            }

            var value = field.Groups["value"].Value.Trim();
            switch (field.Groups["name"].Value.ToUpperInvariant())
            {
                case "FROM":
                    from ??= value;
                    break;
                case "SENT":
                case "DATE":
                    sent ??= value;
                    break;
                case "TO":
                    to ??= value;
                    break;
                case "CC":
                    copied.Add(value);
                    break;
                case "SUBJECT":
                    subject ??= value;
                    break;
            }
        }

        return new HeaderFields(from, sent, to, copied.Count == 0 ? null : string.Join("; ", copied), subject);
    }

    /// <summary>
    /// The name and address on a From value: "Name &lt;address&gt;", Outlook's
    /// "Name &lt;address&lt;mailto:address&gt;&gt;", a bare address, or a name
    /// alone. Either is null when the value does not carry it.
    /// </summary>
    private static (string? Name, string? Address) ReadSender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (null, null);
        }

        var addresses = StaffForwardBodyCleaner.HeaderAddresses(value);
        var name = BracketGroupRegex().Replace(value, " ");
        name = BareAddressRegex().Replace(name, " ");
        name = WhitespaceRunRegex().Replace(name, " ").Trim(' ', '"', '\'', ',', ';', '*', '(', ')');
        return (Blank(name), addresses.Count > 0 ? addresses[0] : null);
    }

    private static IReadOnlyList<string> Addresses(string? value) =>
        string.IsNullOrWhiteSpace(value) ? [] : StaffForwardBodyCleaner.HeaderAddresses(value);

    /// <summary>
    /// Reads the date text in one of <see cref="DateFormats"/>, as its wall
    /// clock reads; a trailing zone ("+0000", "GMT") is not applied.
    /// </summary>
    private static bool TryReadDate(string text, out DateTime value)
    {
        value = default;
        var candidate = WhitespaceRunRegex().Replace(ZoneSuffixRegex().Replace(text, string.Empty), " ").Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        foreach (var culture in DateCultures)
        {
            if (DateTime.TryParseExact(candidate, DateFormats, culture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
            {
                value = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
                return true;
            }
        }

        return false;
    }

    private static int LineOf(int[] lineStarts, int offset)
    {
        var found = Array.BinarySearch(lineStarts, offset);
        return found >= 0 ? found : ~found - 1;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record HeaderFields(string? From, string? Sent, string? To, string? Cc, string? Subject);

    private sealed record QuotedHeader(
        int StartLine,
        int EndLine,
        bool QuotesBeneath,
        string? From,
        string? Sent,
        string? To,
        string? Cc,
        string? Subject);

    // Outlook's "-----Original Message-----" and Gmail's
    // "---------- Forwarded message ---------".
    [GeneratedRegex(
        "^\\s*-{2,}\\s*(?:Original Message|Forwarded message)\\s*-{2,}\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    [GeneratedRegex(
        "^\\s*(?<name>From|Sent|Date|To|Cc|Bcc|Subject):[\\t ]*(?<value>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FieldLineRegex();

    [GeneratedRegex("^\\s*On\\s", RegexOptions.CultureInvariant)]
    private static partial Regex AttributionStartRegex();

    [GeneratedRegex("^\\s*On\\s+(?<content>.+?)\\s+wrote:\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributionRegex();

    // A bracketed part of a From value: "<a@b>", Outlook's "<a@b<mailto:a@b>>",
    // or "[mailto:a@b]".
    [GeneratedRegex("<[^<>]*(?:<[^<>]*>[^<>]*)*>|\\[mailto:[^\\]]*\\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BracketGroupRegex();

    [GeneratedRegex("[^\\s<>\"'\\[\\]()]+@[^\\s<>\"'\\[\\]()]+", RegexOptions.CultureInvariant)]
    private static partial Regex BareAddressRegex();

    // A zone after the time: an RFC 2822 offset or a zone name, with any
    // comment in brackets after it.
    [GeneratedRegex(
        "\\s+(?:[+-]\\d{4}|GMT|UTC|UT|BST|Z)(?:\\s*\\([^)]*\\))?\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ZoneSuffixRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRunRegex();
}
