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
    /// Resolve an allowlisted bundled engineer signature key to a data URI. Returns null
    /// when no key is supplied or the key is not one of <see cref="AvailableSignatures"/>.
    /// </summary>
    public string? SignatureDataUri(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var trimmed = key.Trim();
        if (!AvailableSignatures.Contains(trimmed, StringComparer.Ordinal))
        {
            return null;
        }

        var rel = $"brand/signatures/{trimmed}.png";
        return EmbeddedResources.Exists(rel)
            ? "data:image/png;base64," + Convert.ToBase64String(EmbeddedResources.ReadBytes(rel))
            : null;
    }

    /// <summary>
    /// Resolve a validated custom signature image path or data URI. Render requests reach
    /// this method only after attachment-path policy has accepted the custom field.
    /// </summary>
    public static string? CustomSignatureDataUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!File.Exists(trimmed))
        {
            return null;
        }

        var ext = Path.GetExtension(trimmed).TrimStart('.').ToLowerInvariant();
        var mime = ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "webp" => "image/webp",
            _ => "image/png",
        };
        return $"data:{mime};base64," + Convert.ToBase64String(File.ReadAllBytes(trimmed));
    }

    /// <summary>Keys of the engineer signatures bundled with the renderer.</summary>
    public IReadOnlyList<string> AvailableSignatures { get; } =
        new[] { "andy_patterson", "ed_mawdsley", "neil_oreilly" };
}
