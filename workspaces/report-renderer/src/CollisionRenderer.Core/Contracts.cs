using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace CollisionRenderer.Core;

/// <summary>How tightly body content is packed. Used by fit-to-page templates.</summary>
public enum Density
{
    Normal,
    Compact,
    UltraCompact,
}

/// <summary>Whether the renderer may auto-shrink density to fit a page target.</summary>
public enum DensityFit
{
    Auto,
    Fixed,
}

/// <summary>Per-template policy for handling overflow onto extra pages.</summary>
public enum DensityFitProfile
{
    /// <summary>Never auto-shrink. Content simply flows to as many pages as needed.</summary>
    None,

    /// <summary>Try Normal → Compact → Ultra-compact to land within a page target.</summary>
    FitToPages,
}

public sealed record RenderOptions
{
    public DensityFit Fit { get; init; } = DensityFit.Auto;
    public Density Density { get; init; } = Density.Normal;

    /// <summary>Include a base64 copy of the PDF in the result (bounded; off by default).</summary>
    public bool IncludeBase64 { get; init; }

    /// <summary>Max base64 budget; larger artifacts return Base64 = null.</summary>
    public int Base64Limit { get; init; } = 1_000_000;
}

public sealed record RenderRequest
{
    public required string TemplateId { get; init; }
    public required string Json { get; init; }
    public RenderOptions Options { get; init; } = new();

    /// <summary>
    /// Whether raw local filesystem paths are accepted for image/PDF attachment fields.
    /// True for the desktop app and CLI (the user picks their own files); the cloud API
    /// sets this false so a caller cannot make the server read arbitrary local files.
    /// </summary>
    public bool AllowLocalAttachmentPaths { get; init; } = true;
}

public sealed record RenderResult
{
    public required byte[] Pdf { get; init; }
    public int PageCount { get; init; }
    public required string Sha256 { get; init; }
    public Density Density { get; init; }
    public required string EngineVersion { get; init; }
    public required string SuggestedFileName { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public string? Base64 { get; init; }
}

/// <summary>A4 paged-media settings handed to the PDF engine, including running furniture.</summary>
public sealed record PdfPageSettings
{
    public string Format { get; init; } = "A4";
    public string MarginTop { get; init; } = "1mm";
    public string MarginRight { get; init; } = "12mm";
    public string MarginBottom { get; init; } = "22mm";
    public string MarginLeft { get; init; } = "12mm";

    /// <summary>Running header HTML (Chromium template). Null/empty = blank header.</summary>
    public string? HeaderHtml { get; init; }

    /// <summary>Running footer HTML (Chromium template) — the CE strapline + page marker.</summary>
    public string? FooterHtml { get; init; }
    public bool PrintBackground { get; init; } = true;
}

/// <summary>Fully composed, ready-to-print document (HTML + page furniture).</summary>
public sealed record ComposedDocument
{
    public required string Html { get; init; }
    public required PdfPageSettings Page { get; init; }
}

public sealed record TemplateDescriptor
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required Type ModelType { get; init; }

    /// <summary>Embedded Scriban body template, e.g. "templates/fee_note.scriban".</summary>
    public required string TemplateResource { get; init; }

    /// <summary>Embedded sample payload, e.g. "samples/fee_note.json".</summary>
    public required string SampleResource { get; init; }

    public DensityFitProfile DensityProfile { get; init; } = DensityFitProfile.None;

    /// <summary>Page target when DensityProfile = FitToPages.</summary>
    public int FitTargetPages { get; init; } = 1;

    /// <summary>Trailing part of the generated file name (REG_{suffix}.pdf).</summary>
    public string FileNameSuffix { get; init; } = "document";
}

public sealed record ValidationResult
{
    public bool Ok => Errors.Count == 0;
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>Thrown when a payload fails schema/policy validation before render.</summary>
public sealed class RenderValidationException : Exception
{
    public RenderValidationException(IReadOnlyList<string> errors)
        : base("Payload validation failed:\n - " + string.Join("\n - ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

/// <summary>Shared System.Text.Json options: camelCase, tolerant, enum-as-string.</summary>
public static class CrJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        // LenientStringConverter lets string-typed money/mileage fields also accept a bare JSON
        // number (guide_value: 26417), matching the published number|string contract.
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new LenientStringConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Money model fields are string-typed; the fee-note amount is decimal. Reading
        // numbers from strings lets the form write all money fields as text uniformly.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Draft-authoring serialization. Identical to <see cref="Options"/> but writes
    /// non-ASCII characters literally (not as \uXXXX escapes), so placeholder
    /// guillemets and accented text stay human-readable in the JSON editor and remain
    /// detectable by <see cref="PlaceholderScanner"/>.
    /// </summary>
    public static readonly JsonSerializerOptions Relaxed = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        // See Options above: accept a bare JSON number for string-typed money/mileage fields.
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new LenientStringConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Money model fields are string-typed; the fee-note amount is decimal. Reading
        // numbers from strings lets the form write all money fields as text uniformly.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
}
