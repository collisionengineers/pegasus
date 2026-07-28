using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Mcp.Valuation;
using ModelContextProtocol.Server;

namespace CollisionRenderer.Mcp.Tools;

/// <summary>
/// The backward-compatible <c>render_valuation_outputs</c> drop-in: it accepts the same
/// snake_case payload + captures the report-renderer did and returns the same response
/// envelope, so the existing valuation proxy/skill switch over with no changes.
/// </summary>
[McpServerToolType]
public static class ValuationOutputsTool
{
    [McpServerTool(Name = "render_valuation_outputs", Title = "Render valuation report + evidence pack",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description(
        "Render both the market-valuation report and the advert evidence pack from one valuation payload. " +
        "payload is the snake_case valuation object (subject_vehicle, adverts, market_research, conclusion, …). " +
        "captures is an optional list of advert captures, passed through EXACTLY as capture_advert_pages returned " +
        "them: [{ url, status, filename, evidence_path, sha256 }] (file delivery — preferred; the PDF is read from " +
        "the shared evidence directory and the sha256 is verified) or [{ url, status, filename, pdf_base64 }] " +
        "(inline delivery). Each capture is appended after the evidence table. Returns { artifacts, validation } " +
        "where each artifact carries { uri, filename, sha256 }.")]
    public static async Task<JsonNode> RenderValuationOutputs(
        IDocumentRenderer renderer,
        [Description("The snake_case valuation payload.")] JsonElement payload,
        [Description("Optional advert captures list.")] JsonElement? captures = null,
        [Description("Include a bounded base64 copy of each PDF in the result.")] bool includeBase64 = true,
        CancellationToken ct = default)
    {
        // A default(JsonElement) has ValueKind Undefined, which the renderer treats as "no captures".
        return await new ValuationOutputsRenderer(renderer)
            .RenderAsync(payload, captures ?? default, includeBase64, ct)
            .ConfigureAwait(false);
    }
}
