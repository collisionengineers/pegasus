using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Core.Rendering;
using CollisionRenderer.Mcp.Tools;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// The preflight contract: <c>render_health</c> must say ok:true when a probe PDF can be
/// produced, and ok:false with the underlying error when the engine cannot deliver one —
/// that error is what the skill surfaces INSTEAD of running a whole valuation into a dead
/// renderer.
/// </summary>
public class HealthToolsTests
{
    [Fact]
    public async Task Healthy_engine_reports_ok_with_probe_timing()
    {
        var result = (JsonObject)await HealthTools.RenderHealth(new StubPdfEngine());

        Assert.True(result["ok"]!.GetValue<bool>(), result.ToJsonString());
        var browser = (JsonObject)result["browser"]!;
        Assert.True(browser["probe_ok"]!.GetValue<bool>());
        Assert.True(browser["probe_ms"]!.GetValue<long>() >= 0);
        Assert.Null(browser["error"]);
        Assert.True(result["output_dir_writable"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(result["server_version"]!.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(result["evidence_root"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Failing_engine_reports_not_ok_with_the_underlying_error()
    {
        var result = (JsonObject)await HealthTools.RenderHealth(new ThrowingPdfEngine());

        Assert.False(result["ok"]!.GetValue<bool>());
        var browser = (JsonObject)result["browser"]!;
        Assert.False(browser["probe_ok"]!.GetValue<bool>());
        Assert.Contains("No Chromium-based browser could be launched", browser["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Empty_probe_pdf_reports_not_ok()
    {
        var result = (JsonObject)await HealthTools.RenderHealth(new EmptyPdfEngine());

        Assert.False(result["ok"]!.GetValue<bool>());
        Assert.False(((JsonObject)result["browser"]!)["probe_ok"]!.GetValue<bool>());
    }

    private sealed class ThrowingPdfEngine : IPdfEngine
    {
        public string EngineVersion => "throwing/1.0";

        public Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default) =>
            throw new InvalidOperationException(
                "No Chromium-based browser could be launched for PDF rendering. Attempts: bundled: boom.");

        public int CountPages(byte[] pdf) => 0;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyPdfEngine : IPdfEngine
    {
        public string EngineVersion => "empty/1.0";

        public Task<byte[]> RenderHtmlToPdfAsync(string html, PdfPageSettings settings, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        public int CountPages(byte[] pdf) => 0;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
