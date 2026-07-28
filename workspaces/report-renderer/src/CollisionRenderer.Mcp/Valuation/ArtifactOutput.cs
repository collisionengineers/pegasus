using System.Text.Json.Nodes;
using CollisionRenderer.Core;

namespace CollisionRenderer.Mcp.Valuation;

/// <summary>
/// Persists a rendered PDF to a per-user output directory and builds the artifact
/// descriptor the valuation workflow consumes. The descriptor keys are snake_case to
/// stay byte-compatible with the report-renderer service this host replaces
/// (<c>artifact_id, kind, filename, media_type, bytes, sha256, uri, engine_version,
/// base64, expires_at</c>). There is no remote artifact store locally, so <c>uri</c> is
/// a <c>file://</c> URL to a real file on disk that the host can open directly.
/// </summary>
public static class ArtifactOutput
{
    /// <summary>Cap inline base64 so a large PDF doesn't bloat the JSON-RPC response; the file uri carries it instead.</summary>
    private const int Base64Limit = 1_000_000;

    public static string OutputRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CollisionRenderer",
        "output");

    public static JsonObject Write(RenderResult result, string kind, bool includeBase64)
    {
        Directory.CreateDirectory(OutputRoot);
        var path = WriteToAvailablePath(result.Pdf, Path.Combine(OutputRoot, result.SuggestedFileName));

        return new JsonObject
        {
            ["artifact_id"] = "cr_" + Guid.NewGuid().ToString("N"),
            ["kind"] = kind,
            ["filename"] = Path.GetFileName(path),
            ["media_type"] = "application/pdf",
            ["bytes"] = result.Pdf.Length,
            ["sha256"] = result.Sha256,
            ["uri"] = new Uri(path).AbsoluteUri,
            ["engine_version"] = result.EngineVersion,
            ["base64"] = includeBase64 ? Base64Bounded(result.Pdf) : null,
            ["expires_at"] = null,
        };
    }

    private static JsonNode? Base64Bounded(byte[] pdf)
    {
        // base64 length is ~4/3 of byte length; skip encoding when it would exceed the budget.
        if ((long)pdf.Length * 4 / 3 > Base64Limit)
        {
            return null;
        }

        return JsonValue.Create(Convert.ToBase64String(pdf));
    }

    /// <summary>
    /// Write the PDF to <paramref name="preferredPath"/>, or the first de-collided sibling
    /// (REG_doc_2.pdf, …) that is free, and return the path used. Creation is atomic
    /// (<see cref="FileMode.CreateNew"/>): two concurrent renders of the same registration can
    /// never pick the same name and clobber each other — the previous check-then-write raced
    /// under parallel load (and would throw a sharing violation mid-render).
    /// </summary>
    private static string WriteToAvailablePath(byte[] pdf, string preferredPath)
    {
        var dir = Path.GetDirectoryName(preferredPath) ?? OutputRoot;
        var stem = Path.GetFileNameWithoutExtension(preferredPath);
        var ext = Path.GetExtension(preferredPath);

        for (var i = 1; i < 10_000; i++)
        {
            var candidate = i == 1 ? preferredPath : Path.Combine(dir, $"{stem}_{i}{ext}");
            try
            {
                using var stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(pdf, 0, pdf.Length);
                return candidate;
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Name already taken (possibly by a concurrent render) — try the next suffix.
            }
        }

        // Astronomically unlikely; fall back to a guid suffix rather than overwrite.
        var fallback = Path.Combine(dir, $"{stem}_{Guid.NewGuid():N}{ext}");
        File.WriteAllBytes(fallback, pdf);
        return fallback;
    }
}
