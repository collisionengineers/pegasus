using System.Text.Json;
using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Templating;

namespace CollisionRenderer.Core;

/// <summary>Outcome of a live-preview compose. Tolerant by design: a partial or
/// invalid draft yields a friendly best-effort page rather than throwing.</summary>
public sealed record PreviewResult
{
    /// <summary>A complete, self-contained HTML document ready for an isolated
    /// preview surface.</summary>
    public required string Html { get; init; }

    /// <summary>True when the HTML is a placeholder/last-resort page rather than a
    /// faithful render of the draft (empty, mid-edit, or not yet valid).</summary>
    public bool IsBestEffort { get; init; }

    /// <summary>Optional short note explaining why the preview is best-effort.</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Produces the same body HTML the PDF renderer uses, but for a live, as-you-type
/// preview: it skips validation and never throws, so half-finished drafts still show
/// something. The slow Chromium PDF step is intentionally not involved.
/// </summary>
public interface IPreviewComposer
{
    PreviewResult ComposePreview(string renderTemplateId, string json, Density density);
}

public sealed class PreviewComposer : IPreviewComposer
{
    private readonly IHtmlComposer _composer;
    private readonly ITemplateCatalog _catalog;
    private readonly BrandAssets _brand;

    public PreviewComposer(IHtmlComposer composer, ITemplateCatalog catalog, BrandAssets brand)
    {
        _composer = composer;
        _catalog = catalog;
        _brand = brand;
    }

    public PreviewResult ComposePreview(string renderTemplateId, string json, Density density)
    {
        if (!_catalog.TryGet(renderTemplateId, out var descriptor) || descriptor is null)
        {
            return Placeholder("Choose a document type to start the preview.");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Placeholder("Start filling in the form to see a live preview.");
        }

        object? model;
        try
        {
            model = JsonSerializer.Deserialize(json, descriptor.ModelType, CrJson.Options);
        }
        catch (JsonException)
        {
            return Placeholder("Waiting for valid content...");
        }

        if (model is null)
        {
            return Placeholder("Start filling in the form to see a live preview.");
        }

        try
        {
            var composed = _composer.Compose(descriptor, model, density);
            return new PreviewResult { Html = composed.Html };
        }
        catch (Exception)
        {
            // Half-built nested models (a null inner object, a not-yet-typed block)
            // can throw deep in composition; fall back rather than crash the preview.
            return Placeholder("The preview will refresh once the document has enough content.");
        }
    }

    private PreviewResult Placeholder(string message) => new()
    {
        Html = PlaceholderHtml(message),
        IsBestEffort = true,
        Note = message,
    };

    private string PlaceholderHtml(string message) =>
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<style>" +
        "html,body{height:100%;margin:0;}" +
        "body{display:flex;align-items:center;justify-content:center;" +
        "font-family:Arial,Helvetica,sans-serif;background:#fafafa;color:#555;}" +
        ".ph{max-width:320px;text-align:center;padding:24px;}" +
        ".ph img{width:120px;height:auto;opacity:.92;margin-bottom:18px;}" +
        ".ph p{font-size:13px;line-height:1.5;margin:0;}" +
        "</style></head><body><div class=\"ph\">" +
        $"<img src=\"{_brand.LogoDataUri}\" alt=\"Collision Engineers\">" +
        $"<p>{Format.Enc(message)}</p>" +
        "</div></body></html>";
}
