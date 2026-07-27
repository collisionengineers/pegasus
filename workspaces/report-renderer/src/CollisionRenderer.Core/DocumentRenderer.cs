using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CollisionRenderer.Core.Models;
using CollisionRenderer.Core.Rendering;
using CollisionRenderer.Core.Templating;

namespace CollisionRenderer.Core;

/// <summary>
/// The single rendering entry point used by the CLI, the desktop app and the
/// cloud API. Deserialises a payload, validates it, composes HTML and drives the
/// PDF engine — applying density auto-fit so longer documents flow cleanly
/// instead of garbling the layout.
/// </summary>
public interface IDocumentRenderer : IAsyncDisposable
{
    Task<RenderResult> RenderAsync(RenderRequest request, CancellationToken ct = default);
}

public sealed class DocumentRenderer : IDocumentRenderer
{
    private readonly ITemplateCatalog _catalog;
    private readonly IHtmlComposer _composer;
    private readonly IPayloadValidator _validator;
    private readonly IPdfEngine _engine;
    private readonly bool _ownsEngine;

    public DocumentRenderer(
        ITemplateCatalog catalog,
        IHtmlComposer composer,
        IPayloadValidator validator,
        IPdfEngine engine,
        bool ownsEngine = true)
    {
        _catalog = catalog;
        _composer = composer;
        _validator = validator;
        _engine = engine;
        _ownsEngine = ownsEngine;
    }

    public async Task<RenderResult> RenderAsync(RenderRequest request, CancellationToken ct = default)
    {
        var descriptor = _catalog.Get(request.TemplateId);
        var model = Deserialize(request.Json, descriptor.ModelType);

        var validation = _validator.Validate(descriptor.Id, model, request.AllowLocalAttachmentPaths);
        if (!validation.Ok)
        {
            throw new RenderValidationException(validation.Errors);
        }

        var warnings = new List<string>(validation.Warnings);

        var densities = ResolveDensities(descriptor, request.Options);
        byte[] pdf = Array.Empty<byte>();
        var pageCount = 0;
        var usedDensity = densities[0];

        foreach (var density in densities)
        {
            ct.ThrowIfCancellationRequested();
            var composed = _composer.Compose(descriptor, model, density);
            pdf = await _engine.RenderHtmlToPdfAsync(composed.Html, composed.Page, ct).ConfigureAwait(false);
            pageCount = _engine.CountPages(pdf);
            usedDensity = density;

            if (descriptor.DensityProfile != DensityFitProfile.FitToPages ||
                pageCount <= descriptor.FitTargetPages)
            {
                break;
            }
        }

        if (descriptor.DensityProfile == DensityFitProfile.FitToPages &&
            pageCount > descriptor.FitTargetPages)
        {
            warnings.Add(request.Options.Fit == DensityFit.Auto
                ? $"Content exceeds the {descriptor.FitTargetPages}-page target even at {usedDensity} density; " +
                  $"rendered cleanly across {pageCount} pages."
                : $"Content exceeds the {descriptor.FitTargetPages}-page target at the fixed {usedDensity} density " +
                  $"(auto-fit disabled); rendered cleanly across {pageCount} pages.");
        }

        if (model is AdvertEvidencePackDocument evidencePack)
        {
            var capturedPdfs = evidencePack.Adverts
                .Select(a => a.CapturedPdfPath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (capturedPdfs.Count > 0)
            {
                pdf = PdfEvidenceAppender.Append(pdf, capturedPdfs);
                pageCount = _engine.CountPages(pdf);
                warnings.Add(capturedPdfs.Count == 1
                    ? "Appended 1 captured advert PDF after the evidence table."
                    : $"Appended {capturedPdfs.Count} captured advert PDFs after the evidence table.");
            }
        }

        var sha = Sha256Hex(pdf);
        var fileName = BuildFileName(descriptor, model);
        var base64 = ResolveBase64(pdf, request.Options);

        return new RenderResult
        {
            Pdf = pdf,
            PageCount = pageCount,
            Sha256 = sha,
            Density = usedDensity,
            EngineVersion = $"{_engine.EngineVersion}; template={descriptor.Id}",
            SuggestedFileName = fileName,
            Warnings = warnings,
            Base64 = base64,
        };
    }

    private static List<Density> ResolveDensities(TemplateDescriptor d, RenderOptions options)
    {
        if (options.Fit == DensityFit.Fixed)
        {
            return new List<Density> { options.Density };
        }

        return d.DensityProfile == DensityFitProfile.FitToPages
            ? new List<Density> { Density.Normal, Density.Compact, Density.UltraCompact }
            : new List<Density> { Density.Normal };
    }

    private object Deserialize(string json, Type type)
    {
        try
        {
            var model = JsonSerializer.Deserialize(json, type, CrJson.Options);
            return model ?? throw new RenderValidationException(new[] { "Payload deserialised to null." });
        }
        catch (JsonException ex)
        {
            throw new RenderValidationException(new[] { $"Invalid JSON payload: {ex.Message}" });
        }
    }

    private static string BuildFileName(TemplateDescriptor d, object model)
    {
        var key = model switch
        {
            MarketValuationEvidenceDocument m => m.Subject.Registration,
            AdvertEvidencePackDocument m => m.Subject.Registration,
            FeeNoteDocument m => m.Subject?.Registration ?? m.FeeNoteNumber,
            ExpertReportDocument m => m.Meta.OurRef ?? m.Title,
            _ => "document",
        };

        var slug = Slug(key);
        return $"{slug}_{d.FileNameSuffix}.pdf";
    }

    private static string Slug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "DOCUMENT";
        }

        var chars = value.Trim().ToUpperInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();
        var slug = new string(chars).Trim('_');
        while (slug.Contains("__"))
        {
            slug = slug.Replace("__", "_");
        }

        return slug.Length == 0 ? "DOCUMENT" : slug;
    }

    private static string? ResolveBase64(byte[] pdf, RenderOptions options)
    {
        if (!options.IncludeBase64)
        {
            return null;
        }

        // base64 is ~4/3 of byte length; keep the whole result under the budget.
        return (pdf.Length * 4 / 3) > options.Base64Limit ? null : Convert.ToBase64String(pdf);
    }

    private static string Sha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_ownsEngine)
        {
            await _engine.DisposeAsync().ConfigureAwait(false);
        }
    }
}
