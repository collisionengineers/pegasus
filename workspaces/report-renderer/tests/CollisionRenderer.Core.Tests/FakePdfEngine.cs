using System.Text;
using CollisionRenderer.Core.Rendering;

namespace CollisionRenderer.Core.Tests;

/// <summary>
/// A deterministic, browser-free PDF engine for unit tests. It "renders" by
/// returning the HTML bytes and reports a page count derived from the body's
/// density class — letting us exercise the density auto-fit loop without Chromium.
/// </summary>
internal sealed class FakePdfEngine : IPdfEngine
{
    public List<string> RenderedHtml { get; } = new();

    /// <summary>
    /// Maps the rendered body density to a page count (default ultra=1, compact=2,
    /// normal=3). Matches the <c>&lt;body class="..."&gt;</c> attribute specifically —
    /// the stylesheet itself also mentions the class names, so a loose Contains would
    /// match the CSS rather than the active density.
    /// </summary>
    public Func<string, int> PageCountForHtml { get; set; } = html =>
        html.Contains("class=\"report-ultra-compact\"") ? 1
        : html.Contains("class=\"report-compact\"") ? 2
        : 3;

    public string EngineVersion => "fake/1.0";

    public Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default)
    {
        RenderedHtml.Add(html);
        return Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    public int CountPages(byte[] pdf) => PageCountForHtml(Encoding.UTF8.GetString(pdf));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
