namespace CollisionRenderer.Core.Design;

/// <summary>
/// Brand surface for the document renderer: the embedded letterhead logo, signature
/// images, and the canonical print stylesheet. One instance is shared by every render
/// so the look is identical across CLI, desktop and cloud.
/// </summary>
public sealed class BrandAssets
{
    public static readonly BrandAssets Default = new();

    public BrandAssets()
    {
        LogoDataUri = "data:image/png;base64," +
                      Convert.ToBase64String(EmbeddedResources.ReadBytes("brand/logo.png"));
        Css = EmbeddedResources.ReadText("templates/report.css");
    }

    /// <summary>Master red gear-"C" letterhead logo, inlined as a data URI.</summary>
    public string LogoDataUri { get; }

    /// <summary>The full print stylesheet (A4 paged-media, tables, value box, fee note, signatures).</summary>
    public string Css { get; }

    /// <summary>
    /// Resolve an engineer signature image to a data URI. Keys map to
    /// <c>Assets/brand/signatures/{key}.png</c> (e.g. "andy_patterson"). Returns null
    /// when no key is supplied or the named signature is not bundled.
    /// </summary>
    public string? SignatureDataUri(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var trimmed = key.Trim();
        if (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (File.Exists(trimmed))
        {
            var ext = Path.GetExtension(trimmed).TrimStart('.').ToLowerInvariant();
            var mime = ext switch
            {
                "jpg" or "jpeg" => "image/jpeg",
                "webp" => "image/webp",
                _ => "image/png",
            };
            return $"data:{mime};base64," + Convert.ToBase64String(File.ReadAllBytes(trimmed));
        }

        var rel = $"brand/signatures/{trimmed}.png";
        return EmbeddedResources.Exists(rel)
            ? "data:image/png;base64," + Convert.ToBase64String(EmbeddedResources.ReadBytes(rel))
            : null;
    }

    /// <summary>Keys of the engineer signatures bundled with the renderer.</summary>
    public IReadOnlyList<string> AvailableSignatures { get; } = new[]
    {
        "andy_patterson", "ed_mawdsley", "neil_oreilly",
    };
}
