using System.Text;
using System.Text.RegularExpressions;

namespace CollisionRenderer.Core.Rendering;

/// <summary>
/// Counts pages in a PDF without a third-party library. Chromium writes the page
/// tree as plain indirect objects, so the root <c>/Pages</c> node's <c>/Count</c>
/// (the largest count in the file) gives the page total; we fall back to counting
/// <c>/Type /Page</c> objects. Only used to drive density auto-fit, so a rare
/// off-by-one on exotic input is harmless.
/// </summary>
internal static class PdfPageCounter
{
    private static readonly Regex CountRx = new(@"/Count\s+(\d+)", RegexOptions.Compiled);
    private static readonly Regex PageRx = new(@"/Type\s*/Page(?![a-zA-Z])", RegexOptions.Compiled);

    public static int Count(byte[] pdf)
    {
        // Latin1 maps each byte to one char — safe for scanning ASCII tokens in binary.
        var text = Encoding.Latin1.GetString(pdf);

        var counts = CountRx.Matches(text)
            .Select(m => int.TryParse(m.Groups[1].Value, out var n) ? n : 0)
            .ToList();
        if (counts.Count > 0 && counts.Max() > 0)
        {
            return counts.Max();
        }

        var pages = PageRx.Matches(text).Count;
        return pages > 0 ? pages : 1;
    }
}
