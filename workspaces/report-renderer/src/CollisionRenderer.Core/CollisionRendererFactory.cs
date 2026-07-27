using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Rendering;
using CollisionRenderer.Core.Templating;

namespace CollisionRenderer.Core;

/// <summary>
/// Composition root. Every host (CLI, desktop, API) builds its renderer here, so
/// they share one identical pipeline — the guarantee behind GUI/CLI feature parity.
/// </summary>
public static class CollisionRendererFactory
{
    /// <summary>The shared, stateless template catalog.</summary>
    public static ITemplateCatalog Catalog => TemplateCatalog.Default;

    /// <summary>The shared authoring catalog for blank form templates and draft payloads.</summary>
    public static IAuthoringTemplateCatalog AuthoringCatalog => AuthoringTemplateCatalog.Default;

    /// <summary>
    /// Build a fast, stateless live-preview composer. It reuses the same HTML composer
    /// the renderer uses (so the preview matches the PDF body), but skips validation
    /// and the Chromium step so it is safe to call on every keystroke.
    /// </summary>
    public static IPreviewComposer CreatePreviewComposer()
    {
        var brand = BrandAssets.Default;
        var catalog = TemplateCatalog.Default;
        return new PreviewComposer(new HtmlComposer(brand, catalog), catalog, brand);
    }

    /// <summary>
    /// Build a renderer. Pass a custom <paramref name="engine"/> to swap the PDF
    /// backend (e.g. a fake in tests); otherwise the Chromium engine is used and
    /// owned/disposed by the returned renderer.
    /// </summary>
    public static IDocumentRenderer CreateRenderer(IPdfEngine? engine = null)
    {
        var brand = BrandAssets.Default;
        var catalog = TemplateCatalog.Default;
        var composer = new HtmlComposer(brand, catalog);
        var validator = new PayloadValidator();

        if (engine is null)
        {
            return new DocumentRenderer(catalog, composer, validator, new ChromiumPdfEngine(), ownsEngine: true);
        }

        return new DocumentRenderer(catalog, composer, validator, engine, ownsEngine: false);
    }
}
