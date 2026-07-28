using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;

namespace CollisionRenderer.Mcp.Valuation;

/// <summary>
/// The drop-in replacement for the report-renderer's <c>render_valuation_outputs</c>.
/// Renders both the market-valuation report and the advert evidence pack from one
/// snake_case payload (+ captures) and returns the same response envelope the Python
/// service did, so the valuation workflow's existing proxy and skill consume it unchanged.
///
/// <para>Shared by the MCP stdio tool and (Wave 4) the HTTP <c>/render/valuation_outputs</c>
/// endpoint — the contract and validation live here, once.</para>
/// </summary>
public sealed class ValuationOutputsRenderer
{
    private const string ReportTemplate = "market-valuation-evidence";
    private const string PackTemplate = "advert-evidence-pack";

    private readonly IDocumentRenderer _renderer;

    public ValuationOutputsRenderer(IDocumentRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>
    /// Render report + pack. <paramref name="captures"/> may be <see cref="JsonValueKind.Undefined"/>
    /// (omitted) or a JSON array. Returns one of the report-renderer-compatible envelopes:
    /// success <c>{artifacts,validation:{ok:true,…}}</c>, validation failure
    /// <c>{artifacts:[],validation:{ok:false,…,errors}}</c>, or internal error
    /// <c>{status:"renderer_error",…}</c>.
    /// </summary>
    public async Task<JsonObject> RenderAsync(
        JsonElement payload,
        JsonElement captures,
        bool includeBase64,
        CancellationToken ct = default)
    {
        var duration = Stopwatch.StartNew();

        // 0. Tolerant unwrap — MCP hosts sometimes hand the tool a JSON-ENCODED STRING
        //    instead of the JSON value itself (observed live 2026-07-02: Claude Desktop
        //    passed the whole valuation payload as one string and the render failed
        //    preflight with "payload must be an object", so the skill fell back to its
        //    offline renderer and the evidence pack was never produced). If the element
        //    is a string that parses as JSON of the expected kind, unwrap it; anything
        //    else still hits the original preflight error.
        payload = UnwrapJsonString(payload, JsonValueKind.Object);
        captures = UnwrapJsonString(captures, JsonValueKind.Array);

        // 1. Preflight — identical checks/messages/order to the proxy's preflight(), so a
        //    direct MCP-tool caller gets the same gate the HTTP path already applied.
        var preflightErrors = Preflight(payload, captures);
        if (preflightErrors.Count > 0)
        {
            return ValidationError(preflightErrors, warnings: new List<string>());
        }

        var warnings = new List<string>();

        try
        {
            BrowserBootstrap.EnsureChromium();

            var captureList = captures.ValueKind == JsonValueKind.Array
                ? Capture.Parse(captures)
                : Array.Empty<Capture>();

            // 3. Render the market-valuation report.
            var reportDoc = ValuationPayloadMapper.ToReportDocument(payload);
            SanitizeUnresolvableImagePaths(reportDoc);
            var reportResult = await _renderer.RenderAsync(
                new RenderRequest
                {
                    TemplateId = ReportTemplate,
                    Json = reportDoc.ToJsonString(),
                    AllowLocalAttachmentPaths = false,
                },
                ct).ConfigureAwait(false);

            // 4. Render the evidence pack (captured advert PDFs resolved + appended).
            var packDoc = ValuationPayloadMapper.ToEvidencePackDocument(payload, captureList, out var packErrors);
            if (packErrors.Count > 0)
            {
                return ValidationError(packErrors, warnings);
            }

            SanitizeUnresolvableImagePaths(packDoc);
            var packResult = await _renderer.RenderAsync(
                new RenderRequest
                {
                    TemplateId = PackTemplate,
                    Json = packDoc.ToJsonString(),
                    AllowLocalAttachmentPaths = false,
                },
                ct).ConfigureAwait(false);

            // 5. Persist + describe both artifacts.
            var reportDescriptor = ArtifactOutput.Write(reportResult, "valuation_report", includeBase64);
            var packDescriptor = ArtifactOutput.Write(packResult, "valuation_evidence_pack", includeBase64);

            var allWarnings = new List<string>(warnings);
            allWarnings.AddRange(reportResult.Warnings);
            foreach (var w in packResult.Warnings)
            {
                if (!allWarnings.Contains(w))
                {
                    allWarnings.Add(w);
                }
            }

            LogSuccess(reportDescriptor, packDescriptor, duration.Elapsed);
            return Ok(new[] { reportDescriptor, packDescriptor }, allWarnings);
        }
        catch (RenderValidationException ex)
        {
            return ValidationError(ex.Errors.ToList(), warnings);
        }
        catch (Exception ex)
        {
            LogRendererError(ex);
            return InternalError(ex);
        }
    }

    /// <summary>
    /// If <paramref name="element"/> is a JSON string whose text parses as JSON of
    /// <paramref name="expected"/> kind, return the parsed value; otherwise return the
    /// element unchanged (so preflight reports the original shape). The parsed root is
    /// cloned because the backing <see cref="JsonDocument"/> is disposed here.
    /// </summary>
    internal static JsonElement UnwrapJsonString(JsonElement element, JsonValueKind expected)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return element;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return element;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == expected)
            {
                return doc.RootElement.Clone();
            }
        }
        catch (JsonException)
        {
            // Not JSON — leave the original string; preflight reports the real shape.
        }

        return element;
    }

    /// <summary>Port of the proxy's <c>preflight()</c> — same messages, same order.</summary>
    internal static List<string> Preflight(JsonElement payload, JsonElement captures)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return new List<string> { "payload must be an object" };
        }

        var errors = new List<string>();
        if (!(payload.TryGetProperty("subject_vehicle", out var subject) && subject.ValueKind == JsonValueKind.Object))
        {
            errors.Add("payload.subject_vehicle must be present");
        }

        if (!(payload.TryGetProperty("adverts", out var adverts) && adverts.ValueKind == JsonValueKind.Array && adverts.GetArrayLength() > 0))
        {
            errors.Add("payload.adverts must be a non-empty list");
        }

        if (!TruthyString(payload, "market_research"))
        {
            errors.Add("payload.market_research must be present");
        }

        if (!TruthyString(payload, "conclusion"))
        {
            errors.Add("payload.conclusion must be present");
        }

        if (captures.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.Array))
        {
            errors.Add("captures must be a list when supplied");
        }

        return errors;
    }

    private static bool TruthyString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var v))
        {
            return false;
        }

        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString()?.Length > 0,
            JsonValueKind.Array => v.GetArrayLength() > 0,
            JsonValueKind.Object => v.EnumerateObject().Any(),
            JsonValueKind.Number => v.TryGetDouble(out var d) && d != 0,
            JsonValueKind.True => true,
            _ => false,
        };
    }

    /// <summary>
    /// Drop advert <c>screenshotPath</c> values that aren't resolvable over a service boundary
    /// (anything that isn't a <c>data:</c> URI or an <c>http(s)</c> URL — i.e. a caller-local file
    /// path the renderer can't read). Mirrors the report-renderer's tolerance: a missing screenshot
    /// never blocks the render, and the captured advert PDF is the pack's real evidence anyway.
    /// </summary>
    private static void SanitizeUnresolvableImagePaths(JsonObject doc)
    {
        if (doc["adverts"] is not JsonArray adverts)
        {
            return;
        }

        foreach (var node in adverts)
        {
            if (node is JsonObject advert
                && advert["screenshotPath"]?.GetValue<string>() is { } path
                && !IsResolvableImageSource(path))
            {
                advert.Remove("screenshotPath");
            }
        }
    }

    private static bool IsResolvableImageSource(string value) =>
        value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // --- response envelopes (byte-compatible with report-renderer) ----------

    private static JsonObject Ok(IEnumerable<JsonObject> artifacts, IEnumerable<string> warnings) => new()
    {
        ["artifacts"] = new JsonArray(artifacts.Cast<JsonNode?>().ToArray()),
        ["validation"] = new JsonObject { ["ok"] = true, ["warnings"] = ToArray(warnings) },
    };

    private static JsonObject ValidationError(List<string> errors, List<string> warnings) => new()
    {
        ["artifacts"] = new JsonArray(),
        ["validation"] = new JsonObject
        {
            ["ok"] = false,
            ["warnings"] = ToArray(warnings),
            ["errors"] = ToArrayWithLog(errors),
        },
    };

    private static JsonObject InternalError(Exception ex) => new()
    {
        ["status"] = "renderer_error",
        ["artifacts"] = new JsonArray(),
        ["validation"] = new JsonObject
        {
            ["ok"] = false,
            ["warnings"] = new JsonArray(),
            ["errors"] = ToArray(new[] { $"renderer internal error ({ex.GetType().Name}): {ex.Message}" }),
        },
    };

    private static JsonArray ToArray(IEnumerable<string> values)
    {
        var arr = new JsonArray();
        foreach (var v in values)
        {
            arr.Add(v);
        }

        return arr;
    }

    private static JsonArray ToArrayWithLog(List<string> errors)
    {
        LogValidationFailed(errors);
        return ToArray(errors);
    }

    private static void LogValidationFailed(IEnumerable<string> errors)
    {
        Console.Error.WriteLine(
            $"[collisionrenderer-mcp] render_valuation_outputs validation_failed: {string.Join(" | ", errors)}");
    }

    private static void LogSuccess(JsonObject reportDescriptor, JsonObject packDescriptor, TimeSpan duration)
    {
        Console.Error.WriteLine(
            "[collisionrenderer-mcp] render_valuation_outputs success: "
            + $"templateIds={ReportTemplate},{PackTemplate}; "
            + $"filenames={FileName(reportDescriptor)},{FileName(packDescriptor)}; "
            + $"duration_ms={(long)duration.TotalMilliseconds}");
    }

    private static void LogRendererError(Exception ex)
    {
        Console.Error.WriteLine(
            $"[collisionrenderer-mcp] render_valuation_outputs renderer_error: {ex.GetType().Name}: {ex.Message}");
    }

    private static string FileName(JsonObject descriptor) =>
        descriptor["filename"]?.GetValue<string>() ?? "<unknown>";
}
