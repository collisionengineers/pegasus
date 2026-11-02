using System.Net;
using System.Text.RegularExpressions;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// The inert text of an HTML-only message body: what a reader would see, never
/// rendered and never resolving a resource. The one conversion both the
/// retained display body and intake's body evidence read, so the two cannot
/// disagree about what an HTML message says.
/// </summary>
/// <remarks>
/// Comments and the head, style and script elements are dropped with their
/// content first: stripping only their tags left a sender's CSS as body text.
/// Block and line-break tags become line ends; every other tag becomes a space.
/// </remarks>
internal static partial class HtmlMailText
{
    public static string ToText(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var text = NonContentRegex().Replace(html, " ");
        text = LineBreakTagRegex().Replace(text, "\n");
        text = TagRegex().Replace(text, " ");
        return WebUtility.HtmlDecode(text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    [GeneratedRegex(
        "<!--.*?-->|<(head|style|script)\\b[^>]*>.*?</\\1\\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NonContentRegex();

    [GeneratedRegex("</?(?:br|p|div|tr|li|h[1-6])\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LineBreakTagRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();
}
