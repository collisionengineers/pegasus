using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// Presentation policy for a Collision Engineers staff forward: the retained
/// inbox body should focus on the work provider's original message, not the
/// forwarder's wrapper. It strips leaked inline-image content-id tokens and, for
/// a staff forward, drops the forwarder preamble and signature that sit above the
/// quoted original, keeping the provider's original from the forwarded header on.
/// </summary>
/// <remarks>
/// A pure text policy: it takes already-decoded text and never touches MIME. The
/// forwarded-header boundary is kept byte-identical to
/// <c>MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex</c> so the
/// display and classification views agree on where a forward begins; the two
/// patterns must be changed together.
/// </remarks>
public static partial class StaffForwardBodyCleaner
{
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
                // forwarder's own preamble and signature; keep the provider's
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

    // Leaked inline-image references: `[cid:token]`, `<cid:token>`, or a bare
    // `cid:token`, including the emptied bracket the removal can leave behind.
    [GeneratedRegex("\\[?<?cid:[^\\]>\\s\"']+>?\\]?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CidTokenRegex();

    // Mirrors MimeKitPdfPigOpenXmlIntakeSourceReader.InlineForwardedHeaderRegex.
    [GeneratedRegex(
        "(?i)(?:\\A|[\r\n])From:[\t ]*(?<from>[^\r\n]+)[\r\n]+Sent:[^\r\n]*[\r\n]+To:[^\r\n]*[\r\n]+Subject:[^\r\n]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex ForwardedHeaderRegex();

    [GeneratedRegex("\\n{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex BlankRunRegex();
}
