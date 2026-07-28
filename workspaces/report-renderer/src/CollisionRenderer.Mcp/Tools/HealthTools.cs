using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using CollisionRenderer.Core.Rendering;
using CollisionRenderer.Mcp.Valuation;
using ModelContextProtocol.Server;

namespace CollisionRenderer.Mcp.Tools;

/// <summary>
/// Preflight surface for the valuation workflow. The browser failure mode this exists for:
/// a staff install whose bundled headless shell cannot launch used to be discovered only at
/// the very END of a valuation (the first <c>render_valuation_outputs</c> call), wasting the
/// whole session. <c>render_health</c> runs a real bounded launch+print probe up front — a
/// directory listing cannot distinguish a corrupt shell from a working one — and warms the
/// engine's browser as a side effect, so the first real render is faster too.
/// </summary>
[McpServerToolType]
public static class HealthTools
{
    /// <summary>Probe budget: cold shell launch (2-5 s) + a one-line PDF, with fallback headroom.</summary>
    private static readonly TimeSpan ProbeBudget = TimeSpan.FromSeconds(15);

    [McpServerTool(Name = "render_health", Title = "Renderer health check",
        ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Preflight the renderer BEFORE starting a valuation: launches the PDF browser (bundled headless shell, " +
        "falling back to system Edge/Chrome) and prints a one-line probe PDF. Returns { ok, server_version, " +
        "browser: { resolution, probe_ms, attempts, error }, browsers_path, driver_present, bundled_shell_present, " +
        "output_dir_writable, evidence_root }. Fast when healthy; if ok:false, surface the error to the user " +
        "instead of proceeding with the valuation.")]
    public static async Task<JsonNode> RenderHealth(IPdfEngine engine, CancellationToken ct = default)
    {
        var browsersPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        var driverDir = Path.Combine(AppContext.BaseDirectory, ".playwright");

        var probe = Stopwatch.StartNew();
        var probeOk = false;
        string? probeError = null;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(ProbeBudget);
            try
            {
                var pdf = await engine.RenderHtmlToPdfAsync(
                    "<html><body>collisionrenderer health probe</body></html>",
                    new CollisionRenderer.Core.PdfPageSettings(),
                    cts.Token).ConfigureAwait(false);
                probeOk = pdf.Length > 0;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                probeError = $"browser launch probe timed out after {ProbeBudget.TotalSeconds:0}s";
            }
            catch (Exception ex)
            {
                probeError = ex.Message;
            }
        }

        probe.Stop();

        var resolution = (engine as ChromiumPdfEngine)?.Resolution;
        var attempts = new JsonArray();
        foreach (var attempt in resolution?.Attempts ?? Array.Empty<string>())
        {
            attempts.Add(attempt);
        }

        var outputWritable = ProbeOutputDirWritable(out var outputError);
        var evidenceRoot = EvidencePathResolver.Root;

        var result = new JsonObject
        {
            ["ok"] = probeOk && outputWritable,
            ["server_version"] = ServerVersion(),
            ["browser"] = new JsonObject
            {
                // "bundled" | "msedge" | "chrome" | "missing" | null (engine not Chromium/stub).
                ["resolution"] = resolution?.Kind,
                ["channel"] = resolution?.Channel,
                ["probe_ok"] = probeOk,
                ["probe_ms"] = (long)probe.Elapsed.TotalMilliseconds,
                ["attempts"] = attempts,
                ["error"] = probeError,
            },
            ["browsers_path"] = string.IsNullOrWhiteSpace(browsersPath) ? null : browsersPath,
            ["browsers_path_exists"] = !string.IsNullOrWhiteSpace(browsersPath) && Directory.Exists(browsersPath),
            ["bundled_shell_present"] = BrowserBootstrap.ChromiumPresent(),
            ["driver_present"] = Directory.Exists(driverDir),
            ["output_dir"] = ArtifactOutput.OutputRoot,
            ["output_dir_writable"] = outputWritable,
            ["output_dir_error"] = outputError,
            ["evidence_root"] = evidenceRoot,
            ["evidence_root_exists"] = Directory.Exists(evidenceRoot),
        };

        Console.Error.WriteLine(
            "[collisionrenderer-mcp] render_health: "
            + $"ok={result["ok"]}; resolution={resolution?.Kind ?? "n/a"}; probe_ms={(long)probe.Elapsed.TotalMilliseconds}"
            + (probeError is null ? string.Empty : $"; error={probeError}"));

        return result;
    }

    private static string ServerVersion() =>
        typeof(HealthTools).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(HealthTools).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    private static bool ProbeOutputDirWritable(out string? error)
    {
        try
        {
            Directory.CreateDirectory(ArtifactOutput.OutputRoot);
            var probeFile = Path.Combine(ArtifactOutput.OutputRoot, $".health-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "probe");
            File.Delete(probeFile);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
