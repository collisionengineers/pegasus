using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Presentation policy for a Collision Engineers staff forward: the retained
/// inbox body should focus on the Principal's original message, not the
/// forwarder's wrapper. It strips leaked inline-image content-id tokens and, for
/// a staff forward, drops the forwarder preamble and signature that sit above the
/// quoted original, keeping the Principal's original from the forwarded header on.
/// </summary>
/// <remarks>
/// A pure text policy: it takes already-decoded text and never touches MIME.
/// The forwarded-header boundary is owned here and exposed as
/// <see cref="ForwardedHeaderPattern"/>, so the display and classification
/// views cannot disagree about where a forward begins (MAIL-011). They read
/// the same compiled pattern; each keeps its own rule about what a match
/// proves.
/// </remarks>
public static partial class StaffForwardBodyCleaner
{
    /// <summary>
    /// The shape of a forwarded message's own header block. The single owner:
    /// intake's source reader matches route evidence with it, and this class
    /// finds the display boundary with it. It was written out twice, in two
    /// projects, held equal only by a comment — which is how a header carrying
    /// a Cc line came to be unreadable in both at once (MAIL-011).
    /// </summary>
    public static Regex ForwardedHeaderPattern => ForwardedHeaderRegex();

    public static string Clean(string body, bool isStaffForward)
    {
        ArgumentNullException.ThrowIfNull(body);
        var text = CidTokenRegex().Replace(body, string.Empty);
        if (isStaffForward)
        {
            var boundary = ForwardedHeaderRegex().Match(text);
            if (boundary.Success && boundary.Index > 0)
            {
                // Everything above the first forwarded header is the CE
                // forwarder's own preamble and signature; keep the Principal's
                // original from the header line onward.
                text = text[boundary.Index..].TrimStart('\r', '\n', ' ', '\t');
            }
        }

        text = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
        return BlankRunRegex().Replace(text, "\n\n").Trim();
    }

    /// <summary>
    /// Splits an already-cleaned body into the leading forwarded
    /// From:/Sent:/To:/Subject: header block and the message that follows it,
    /// so views can quote the header separately and excerpts can skip it.
    /// Bodies that do not begin with the block come back with no header lines.
    /// </summary>
    public static (IReadOnlyList<string> HeaderLines, string Body) SplitForwardedHeader(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var boundary = ForwardedHeaderRegex().Match(body);
        if (!boundary.Success || boundary.Index != 0)
        {
            return ([], body);
        }

        var headerLines = boundary.Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rest = body[(boundary.Index + boundary.Length)..].TrimStart('\r', '\n', ' ', '\t');
        return (headerLines, rest);
    }

    /// <summary>
    /// The subject without the leading "FW:" and "Fwd:" prefixes a staff
    /// forward added, in any case and however many there are; any "Re:" after
    /// them stays. One pass over the text.
    /// </summary>
    public static string WithoutForwardPrefixes(string? subject)
    {
        var value = subject?.Trim() ?? string.Empty;
        var start = 0;
        while (true)
        {
            while (start < value.Length && char.IsWhiteSpace(value[start]))
            {
                start++;
            }

            if (StartsWithAt(value, start, "FW:"))
            {
                start += 3;
            }
            else if (StartsWithAt(value, start, "Fwd:"))
            {
                start += 4;
            }
            else
            {
                return value[start..];
            }
        }

        static bool StartsWithAt(string text, int index, string prefix) =>
            text.Length - index >= prefix.Length
            && string.Compare(text, index, prefix, 0, prefix.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    /// <summary>
    /// The original sender named by a forwarded body's own header block —
    /// the address on its "From:" line. Read straight from the retained
    /// body so the operator surface can name the real sender from the first
    /// paint, instead of showing the forwarding desk until intake
    /// processing writes the authoritative route decision (MAIL-009).
    /// Fails closed: a body with no forwarded header, or a From: line
    /// carrying no address or more than one, yields null rather than a guess.
    /// </summary>
    public static string? ForwardedSenderAddress(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var boundary = ForwardedHeaderRegex().Match(body);
        if (!boundary.Success)
        {
            return null;
        }

        var addresses = HeaderAddresses(boundary.Groups["from"].Value);
        return addresses.Count == 1 ? addresses[0] : null;
    }

    /// <summary>
    /// The addresses on a forwarded body's own "To:" and "Cc:" lines: who the
    /// original message was sent to and copied, so a staff forward is answered
    /// as though the original had arrived directly (operator, 3 November
    /// 2026). Empty when the body has no forwarded header; a part carrying no
    /// address is skipped.
    /// </summary>
    public static (IReadOnlyList<string> To, IReadOnlyList<string> Cc) ForwardedRecipients(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var boundary = ForwardedHeaderRegex().Match(body);
        if (!boundary.Success)
        {
            return ([], []);
        }

        return (
            HeaderAddresses(boundary.Groups["to"].Value),
            HeaderAddresses(string.Join(' ', boundary.Groups["cc"].Captures.Select(capture => capture.Value))));
    }

    /// <summary>
    /// The distinct addresses on one header line, in first-seen order. It
    /// reads address tokens, not angle-bracket contents, so Outlook's
    /// text-body form "Name &lt;a@b&lt;mailto:a@b&gt;&gt;" yields a@b once,
    /// and a line separated by ";" or "," yields each address.
    /// </summary>
    public static IReadOnlyList<string> HeaderAddresses(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return [.. AddressTokenRegex().Matches(line)
            .Select(match => match.Groups["address"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Cuts the Principal's trailing signature footer — image placeholders,
    /// decorated contact links, the corporate disclaimer, membership and
    /// registered-office lines — from an already-cleaned display body. The
    /// boundary is the earliest line matching a measured footer marker
    /// (MAIL-007 corpus research); the sign-off above it stays. Fails open:
    /// a body with no marker, or one that would lose every line, is returned
    /// unchanged. Display-side only — retained and searchable text never
    /// pass through this.
    /// </summary>
    public static string TrimPrincipalFooter(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var lines = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var boundary = -1;
        for (var index = 0; index < lines.Length; index++)
        {
            if (FooterMarkerRegex().IsMatch(lines[index].Trim()))
            {
                boundary = index;
                break;
            }
        }

        if (boundary <= 0)
        {
            return body;
        }

        var kept = lines[..boundary];
        if (!kept.Any(line => !string.IsNullOrWhiteSpace(line)))
        {
            return body;
        }

        return string.Join('\n', kept).TrimEnd('\n', ' ', '\t');
    }

    /// <summary>
    /// Drops every line that is wholly one bracketed token — the placeholder a
    /// plain-text part writes for a signature image: its source URL
    /// (<c>[http://…/logo.gif]</c>) or its alt text (<c>[Company Logo]</c>).
    /// A line with any other text keeps its brackets. Display-side only, and
    /// run after <see cref="TrimPrincipalFooter"/>, which reads an image URL
    /// placeholder as a footer boundary.
    /// </summary>
    public static string WithoutImagePlaceholderLines(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var lines = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Where(line => !ImagePlaceholderLineRegex().IsMatch(line));
        return string.Join('\n', lines);
    }

    // Leaked inline-image references: `[cid:token]`, `<cid:token>`, or a bare
    // `cid:token`, including the emptied bracket the removal can leave behind
    // and the `Description: ` an older Outlook writes before the token.
    [GeneratedRegex("\\[?(?:Description:\\s*)?<?cid:[^\\]>\\s\"']+>?\\]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CidTokenRegex();

    [GeneratedRegex(
        "^\\s*\\[(?:https?://[^\\]\\s]+|[^\\[\\]\\r\\n]{1,80})\\]\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImagePlaceholderLineRegex();

    // MAIL-011: Outlook writes Cc:, and can write Bcc:, between To: and
    // Subject:. Allowing them is what lets a forward with a copied recipient
    // be read at all; the block must still be an address header ending in
    // Subject:, so the boundary is no less specific than before.
    [GeneratedRegex(
        "(?i)(?:\\A|[\r\n])From:[\t ]*(?<from>[^\r\n]+)[\r\n]+Sent:[^\r\n]*[\r\n]+To:(?<to>[^\r\n]*)"
        + "(?:[\r\n]+(?:Cc:(?<cc>[^\r\n]*)|Bcc:[^\r\n]*))*[\r\n]+Subject:[^\r\n]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex ForwardedHeaderRegex();

    [GeneratedRegex("\\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankRunRegex();

    // One address token wherever it sits on a header line: bare, inside
    // "<…>", or after Outlook's "mailto:".
    [GeneratedRegex(
        "(?i)(?<![a-z0-9._%+-])(?<address>[a-z0-9._%+-]+@[a-z0-9.-]+\\.[a-z]{2,})(?![a-z0-9._%+-])",
        RegexOptions.CultureInvariant)]
    private static partial Regex AddressTokenRegex();

    // The measured footer boundary markers (MAIL-007): a line that is an
    // image placeholder, carries a decorated contact link, or opens the
    // Principal's disclaimer/membership/registration block.
    [GeneratedRegex(
        "(?i)^\\[(?:https?://|cid:)"
        + "|<(?:tel:|mailto:|https?://)"
        + "|^you are dealing with\\b"
        + "|^this e-?mail (?:and any attachments|is confidential)"
        + "|^the registered office\\b"
        + "|^proud members? of\\b"
        + "|^disclaimer\\b"
        + "|confidential and intended (?:solely|only)",
        RegexOptions.CultureInvariant)]
    private static partial Regex FooterMarkerRegex();
}
