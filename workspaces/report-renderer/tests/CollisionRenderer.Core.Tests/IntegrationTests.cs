using System.Text;
using CollisionRenderer.Core.Rendering;
using Xunit;

namespace CollisionRenderer.Core.Tests;

/// <summary>
/// End-to-end renders through real headless Chromium. These are skipped
/// automatically when the browser is not installed, so the suite stays green in
/// environments without it (run `collisionrenderer install-browser` to enable).
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests
{
    [Theory]
    [MemberData(nameof(AllTemplateIds))]
    public async Task Renders_a_real_pdf(string id)
    {
        await using var renderer = CollisionRendererFactory.CreateRenderer();

        RenderResult result;
        try
        {
            result = await renderer.RenderAsync(new RenderRequest
            {
                TemplateId = id,
                Json = CollisionRendererFactory.AuthoringCatalog.GetStarterJson(id),
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Chromium"))
        {
            return; // browser not installed — treat as skipped
        }

        Assert.True(result.PageCount >= 1);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(result.Pdf, 0, 4));
        Assert.Equal(64, result.Sha256.Length);
    }

    [Fact]
    public async Task Cold_browser_startup_honors_cancellation()
    {
        await using var engine = new ChromiumPdfEngine();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                engine.RenderHtmlToPdfAsync(
                    "<!doctype html><html><body>probe</body></html>",
                    new PdfPageSettings(),
                    cancellation.Token));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Chromium"))
        {
            return;
        }

        Assert.True(
            elapsed.Elapsed < TimeSpan.FromSeconds(5),
            $"Cancellation took {elapsed.Elapsed}.");
    }

    public static IEnumerable<object[]> AllTemplateIds() =>
        CollisionRendererFactory.Catalog.List().Select(t => new object[] { t.Id });
}
