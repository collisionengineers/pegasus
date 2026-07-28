using System.Security.Cryptography;

namespace CollisionRenderer.Mcp.Valuation;

/// <summary>
/// Resolves a capture's <c>evidence_path</c> — a PDF the valuation connector wrote into the
/// shared per-user evidence directory — into bytes the evidence pack can append.
///
/// <para>This is deliberately NOT a relaxation of the render pipeline's
/// <c>AllowLocalAttachmentPaths=false</c> stance (arbitrary client-supplied local paths stay
/// rejected). It is a narrow, allowlisted exception with an integrity binding: the path must
/// live under the canonical evidence root that only the capture connector writes to, carry a
/// <c>.pdf</c> extension, fit a size cap, and match a <b>mandatory</b> sha256 the connector
/// computed at capture time — so the model relaying <c>{evidence_path, sha256}</c> between the
/// two servers cannot point the renderer at any other file, nor at altered bytes.</para>
/// </summary>
public static class EvidencePathResolver
{
    /// <summary>Per-capture size cap; captures target ~145 KB, so 2 MB is generous headroom.</summary>
    internal const long MaxBytes = 2_000_000;

    /// <summary>
    /// The shared evidence root: <c>%LOCALAPPDATA%\CollisionEngineers\evidence</c>, overridable
    /// via <c>COLLISIONRENDERER_EVIDENCE_ROOT</c> (tests, non-standard installs). Must match the
    /// valuation connector's root (its override: <c>VALUATIONBOT_EVIDENCE_ROOT</c>) — the
    /// convention is documented in contracts/schemas/valuation/v1/evidence-transfer.md.
    /// </summary>
    public static string Root
    {
        get
        {
            var overridden = Environment.GetEnvironmentVariable("COLLISIONRENDERER_EVIDENCE_ROOT");
            if (!string.IsNullOrWhiteSpace(overridden))
            {
                return Path.GetFullPath(overridden);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CollisionEngineers",
                "evidence");
        }
    }

    /// <summary>
    /// Try to read an evidence PDF. On success returns true with <paramref name="base64"/> set;
    /// on failure returns false with an actionable <paramref name="error"/> (naming the URL is
    /// the caller's job — these messages describe the file problem).
    /// </summary>
    public static bool TryResolve(string evidencePath, string? sha256, out string base64, out string? error)
    {
        base64 = string.Empty;

        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            error = "evidence_path is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(sha256))
        {
            error = "captures[].sha256 is required alongside evidence_path (the connector returns it with every file capture)";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(evidencePath);
        }
        catch (Exception)
        {
            error = $"evidence_path is not a valid path: {evidencePath}";
            return false;
        }

        if (!IsUnderRoot(fullPath, Root))
        {
            error = $"evidence_path is outside the shared evidence root ({Root}) — only files written by the "
                + "valuation connector's capture_advert_pages can be attached";
            return false;
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            error = "evidence_path must point at a .pdf file";
            return false;
        }

        FileInfo info;
        try
        {
            info = new FileInfo(fullPath);
        }
        catch (Exception ex)
        {
            error = $"evidence file could not be read ({ex.GetType().Name}) — re-capture the URL";
            return false;
        }

        if (!info.Exists)
        {
            error = "evidence file missing — it may have been cleaned up; re-capture the URL";
            return false;
        }

        if (info.Length > MaxBytes)
        {
            error = $"evidence file is too large ({info.Length} bytes > {MaxBytes}) — re-capture the URL";
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex)
        {
            error = $"evidence file could not be read ({ex.GetType().Name}) — re-capture the URL";
            return false;
        }

        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(actual, sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            error = "evidence file sha256 mismatch — the file changed since capture (stale or tampered); re-capture the URL";
            return false;
        }

        base64 = Convert.ToBase64String(bytes);
        error = null;
        return true;
    }

    /// <summary>
    /// Windows-safe containment check: compare canonical full paths, case-insensitively, with a
    /// trailing separator so <c>…\evidenceEvil</c> never passes as inside <c>…\evidence</c>.
    /// </summary>
    internal static bool IsUnderRoot(string fullPath, string root)
    {
        var canonicalRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase);
    }
}
