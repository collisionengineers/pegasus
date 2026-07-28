using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Mcp.Valuation;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// Shared temp evidence root for the file-handoff tests. These tests mutate the
/// process-wide <c>COLLISIONRENDERER_EVIDENCE_ROOT</c> env var, so every class using it
/// sits in one xunit collection and restores state on dispose.
/// </summary>
public sealed class EvidenceRootFixture : IDisposable
{
    private readonly string? _savedRoot = Environment.GetEnvironmentVariable("COLLISIONRENDERER_EVIDENCE_ROOT");

    public EvidenceRootFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "cr-evidence-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Environment.SetEnvironmentVariable("COLLISIONRENDERER_EVIDENCE_ROOT", Root);
    }

    public string Root { get; }

    /// <summary>Write a valid evidence PDF under the root; returns (path, sha256).</summary>
    public (string Path, string Sha256) WritePdf(string relative, byte[]? bytes = null)
    {
        bytes ??= StubPdfEngine.MakeOnePagePdf();
        var path = System.IO.Path.Combine(Root, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return (path, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("COLLISIONRENDERER_EVIDENCE_ROOT", _savedRoot);
        try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
    }
}

[CollectionDefinition("EvidenceRootEnv")]
public sealed class EvidenceRootCollection : ICollectionFixture<EvidenceRootFixture>;

/// <summary>
/// The allowlist + integrity gate for connector-written evidence files. Anything that is
/// not (inside the root) ∧ (.pdf) ∧ (exists) ∧ (≤ cap) ∧ (sha256 supplied AND matching)
/// must be rejected with an actionable message — this is the narrow exception to the
/// render pipeline's blanket local-path rejection, so the edges are the whole test.
/// </summary>
[Collection("EvidenceRootEnv")]
public class EvidencePathResolverTests(EvidenceRootFixture fx)
{
    [Fact]
    public void Resolves_valid_file_and_returns_base64()
    {
        var (path, sha) = fx.WritePdf(Path.Combine("batch-ok", "advert1.pdf"));

        Assert.True(EvidencePathResolver.TryResolve(path, sha, out var base64, out var error), error);
        Assert.Equal(File.ReadAllBytes(path), Convert.FromBase64String(base64));
    }

    [Fact]
    public void Sha256_is_case_insensitive()
    {
        var (path, sha) = fx.WritePdf(Path.Combine("batch-case", "advert1.pdf"));

        Assert.True(EvidencePathResolver.TryResolve(path, sha.ToLowerInvariant(), out _, out var error), error);
    }

    [Fact]
    public void Missing_sha256_is_rejected()
    {
        var (path, _) = fx.WritePdf(Path.Combine("batch-nosha", "advert1.pdf"));

        Assert.False(EvidencePathResolver.TryResolve(path, sha256: null, out _, out var error));
        Assert.Contains("sha256 is required", error);
    }

    [Fact]
    public void Sha256_mismatch_is_rejected()
    {
        var (path, _) = fx.WritePdf(Path.Combine("batch-tamper", "advert1.pdf"));

        Assert.False(EvidencePathResolver.TryResolve(path, new string('a', 64), out _, out var error));
        Assert.Contains("sha256 mismatch", error);
    }

    [Fact]
    public void Path_outside_root_is_rejected()
    {
        var outside = Path.Combine(Path.GetTempPath(), "cr-evidence-outside-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(outside, StubPdfEngine.MakeOnePagePdf());
        try
        {
            var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outside)));
            Assert.False(EvidencePathResolver.TryResolve(outside, sha, out _, out var error));
            Assert.Contains("outside the shared evidence root", error);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void Traversal_out_of_root_is_rejected()
    {
        // A path that LOOKS rooted but escapes via '..' must canonicalise before the check.
        var sneaky = Path.Combine(fx.Root, "batch", "..", "..", "elsewhere.pdf");

        Assert.False(EvidencePathResolver.TryResolve(sneaky, new string('a', 64), out _, out var error));
        Assert.Contains("outside the shared evidence root", error);
    }

    [Fact]
    public void Sibling_directory_with_root_as_prefix_is_rejected()
    {
        // …\evidenceEvil must not pass a naive StartsWith against …\evidence.
        var sibling = fx.Root + "Evil";
        Directory.CreateDirectory(sibling);
        try
        {
            var path = Path.Combine(sibling, "advert1.pdf");
            File.WriteAllBytes(path, StubPdfEngine.MakeOnePagePdf());
            var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

            Assert.False(EvidencePathResolver.TryResolve(path, sha, out _, out var error));
            Assert.Contains("outside the shared evidence root", error);
        }
        finally
        {
            try { Directory.Delete(sibling, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Non_pdf_extension_is_rejected()
    {
        var (pdfPath, _) = fx.WritePdf(Path.Combine("batch-ext", "advert1.pdf"));
        var exePath = Path.ChangeExtension(pdfPath, ".exe");
        File.Copy(pdfPath, exePath, overwrite: true);
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exePath)));

        Assert.False(EvidencePathResolver.TryResolve(exePath, sha, out _, out var error));
        Assert.Contains(".pdf", error);
    }

    [Fact]
    public void Missing_file_is_rejected_with_recapture_hint()
    {
        var gone = Path.Combine(fx.Root, "batch-gone", "advert1.pdf");

        Assert.False(EvidencePathResolver.TryResolve(gone, new string('a', 64), out _, out var error));
        Assert.Contains("re-capture", error);
    }

    [Fact]
    public void Oversized_file_is_rejected()
    {
        var big = new byte[EvidencePathResolver.MaxBytes + 1];
        var (path, sha) = fx.WritePdf(Path.Combine("batch-big", "advert1.pdf"), big);

        Assert.False(EvidencePathResolver.TryResolve(path, sha, out _, out var error));
        Assert.Contains("too large", error);
    }
}

/// <summary>
/// The mapper + full render path over file-handoff captures: <c>{evidence_path, sha256}</c>
/// blocks must behave exactly like inline <c>pdf_base64</c> blocks end-to-end, and a broken
/// reference must fail the render with BOTH the specific cause and the standard
/// missing-captures error.
/// </summary>
[Collection("EvidenceRootEnv")]
public class EvidencePathRenderTests(EvidenceRootFixture fx)
{
    [Fact]
    public void Mapper_resolves_evidence_path_capture_to_data_uri()
    {
        var (path, sha) = fx.WritePdf(Path.Combine("batch-map", "advert1.pdf"));
        var captures = FileCaptures(path, sha);

        var doc = ValuationPayloadMapper.ToEvidencePackDocument(
            ValuationFixtures.Payload(), Capture.Parse(captures), out var errors);

        Assert.Empty(errors);
        var adverts = (JsonArray)doc["adverts"]!;
        foreach (var advert in adverts)
        {
            var captured = advert!["capturedPdfPath"]!.GetValue<string>();
            Assert.StartsWith("data:application/pdf;base64,", captured);
        }
    }

    [Fact]
    public void Mapper_prefers_inline_base64_when_both_are_present()
    {
        var inline = Convert.ToBase64String(StubPdfEngine.MakeOnePagePdf());
        var (path, sha) = fx.WritePdf(Path.Combine("batch-both", "advert1.pdf"), new byte[] { 1, 2, 3 });

        var arr = new JsonArray();
        foreach (var url in ValuationFixtures.AdvertUrls)
        {
            arr.Add(new JsonObject
            {
                ["url"] = url,
                ["status"] = "success",
                ["filename"] = "advert.pdf",
                ["pdf_base64"] = inline,
                ["evidence_path"] = path,
                ["sha256"] = sha,
            });
        }

        var doc = ValuationPayloadMapper.ToEvidencePackDocument(
            ValuationFixtures.Payload(), Capture.Parse(JsonSerializer.SerializeToElement(arr)), out var errors);

        Assert.Empty(errors);
        var captured = ((JsonArray)doc["adverts"]!)[0]!["capturedPdfPath"]!.GetValue<string>();
        Assert.Equal("data:application/pdf;base64," + inline, captured);
    }

    [Fact]
    public async Task Render_succeeds_end_to_end_with_file_captures()
    {
        var (path, sha) = fx.WritePdf(Path.Combine("batch-render", "advert1.pdf"));

        var result = await Render(ValuationFixtures.Payload(), FileCaptures(path, sha));

        Assert.True(result["validation"]!["ok"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Equal(2, ((JsonArray)result["artifacts"]!).Count);
    }

    [Fact]
    public async Task Render_fails_actionably_when_evidence_file_is_missing()
    {
        var gone = Path.Combine(fx.Root, "batch-render-gone", "advert1.pdf");

        var result = await Render(ValuationFixtures.Payload(), FileCaptures(gone, new string('a', 64)));

        Assert.False(result["validation"]!["ok"]!.GetValue<bool>());
        var errors = ((JsonArray)result["validation"]!["errors"]!).Select(e => e!.GetValue<string>()).ToArray();
        Assert.Contains(errors, e => e.Contains("re-capture"));
        Assert.Contains(errors, e => e.Contains("captured advert PDFs"));
    }

    /// <summary>File-mode capture blocks (same shape the connector's file delivery returns).</summary>
    private static JsonElement FileCaptures(string path, string sha)
    {
        var arr = new JsonArray();
        foreach (var url in ValuationFixtures.AdvertUrls)
        {
            arr.Add(new JsonObject
            {
                ["url"] = url,
                ["status"] = "success",
                ["filename"] = Path.GetFileName(path),
                ["evidence_path"] = path,
                ["sha256"] = sha,
            });
        }

        return JsonSerializer.SerializeToElement(arr);
    }

    private static async Task<JsonObject> Render(JsonElement payload, JsonElement captures)
    {
        await using var renderer = CollisionRendererFactory.CreateRenderer(new StubPdfEngine());
        return await new ValuationOutputsRenderer(renderer).RenderAsync(payload, captures, includeBase64: true);
    }
}
