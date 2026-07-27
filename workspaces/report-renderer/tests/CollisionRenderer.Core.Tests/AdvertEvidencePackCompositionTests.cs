using System.Linq;
using System.Text.Json;
using CollisionRenderer.Core;
using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Models;
using CollisionRenderer.Core.Templating;
using Xunit;

namespace CollisionRenderer.Core.Tests;

/// <summary>
/// Locks the advert-evidence-pack layout fix. The pack must stay a clean one-page cover:
/// the CAPTURED EVIDENCE section only appears when an advert carries an inline SCREENSHOT to
/// display; PDF-only captures (the normal workflow) add no near-empty boxes — the captured PDFs
/// are appended after the pack instead. And when the section does render, it is no longer boxed.
/// </summary>
public class AdvertEvidencePackCompositionTests
{
    [Fact]
    public void Pdf_only_captures_render_no_captured_evidence_section()
    {
        var model = Load();
        var pdfOnly = model with
        {
            Adverts = model.Adverts
                .Select(a => a with { CapturedPdfPath = "data:application/pdf;base64,JVBERi0xLjQK" })
                .ToList(),
        };

        var html = Compose(pdfOnly);

        // The heading text and the rendered div both mark the section; ".capture-block" also
        // appears in the embedded CSS, so match class="capture-block" (a rendered div only).
        Assert.DoesNotContain("CAPTURED EVIDENCE", html);
        Assert.DoesNotContain("class=\"capture-block\"", html);
    }

    [Fact]
    public void No_captures_render_no_captured_evidence_section()
    {
        var html = Compose(Load());

        Assert.DoesNotContain("CAPTURED EVIDENCE", html);
    }

    [Fact]
    public void Screenshot_captures_render_the_captured_evidence_section_without_a_box()
    {
        var model = Load();
        var withShot = model with
        {
            Adverts = model.Adverts
                .Select((a, i) => i == 0 ? a with { ScreenshotPath = "data:image/png;base64,iVBORw0KGgo=" } : a)
                .ToList(),
        };

        var html = Compose(withShot);

        Assert.Contains("CAPTURED EVIDENCE", html);
        Assert.Contains("class=\"capture-block\"", html);
        // The box border/padding that made the "random boxes" is gone (was the only 0.8pt border).
        Assert.DoesNotContain("border: 0.8pt solid #BEBEBE", html);
    }

    private static AdvertEvidencePackDocument Load() =>
        JsonSerializer.Deserialize<AdvertEvidencePackDocument>(
            TemplateCatalog.Default.GetSampleJson("advert-evidence-pack"), CrJson.Options)!;

    private static string Compose(AdvertEvidencePackDocument model)
    {
        var composer = new HtmlComposer(BrandAssets.Default, TemplateCatalog.Default);
        return composer.Compose(TemplateCatalog.Default.Get("advert-evidence-pack"), model, Density.Normal).Html;
    }
}
