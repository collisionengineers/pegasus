using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Mcp.Valuation;
using ModelContextProtocol.Server;

namespace CollisionRenderer.Mcp.Tools;

/// <summary>
/// General rendering tools mirroring the REST surface (templates / validate / render),
/// plus a first-run browser installer. The valuation drop-in lives in
/// <see cref="ValuationOutputsTool"/>.
/// </summary>
[McpServerToolType]
public static class RenderTools
{
    [McpServerTool(Name = "list_templates", Title = "List templates",
        ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the document templates this renderer can produce (id, name, description).")]
    public static JsonNode ListTemplates()
    {
        var arr = new JsonArray();
        foreach (var t in CollisionRendererFactory.Catalog.List())
        {
            arr.Add(new JsonObject { ["id"] = t.Id, ["name"] = t.Name, ["description"] = t.Description });
        }

        return arr;
    }

    [McpServerTool(Name = "get_template_sample", Title = "Get template sample",
        ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Return the bundled starter payload (JSON) for a template id.")]
    public static string GetTemplateSample(
        [Description("Template id, e.g. 'market-valuation-evidence'.")] string templateId)
    {
        if (!CollisionRendererFactory.Catalog.TryGet(templateId, out _))
        {
            throw new ArgumentException($"unknown template '{templateId}'");
        }

        return CollisionRendererFactory.Catalog.GetSampleJson(templateId);
    }

    [McpServerTool(Name = "validate", Title = "Validate payload",
        ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Validate a payload against a template's schema/policy without rendering. Returns { ok, errors, warnings }.")]
    public static JsonNode Validate(
        [Description("Template id.")] string templateId,
        [Description("The document payload object for that template.")] JsonElement data)
    {
        if (!CollisionRendererFactory.Catalog.TryGet(templateId, out var d))
        {
            var errors = new[] { $"unknown template '{templateId}'" };
            LogValidateOutcome(templateId, ok: false, errors);
            return new JsonObject { ["ok"] = false, ["errors"] = ToArray(errors) };
        }

        try
        {
            var model = JsonSerializer.Deserialize(data.GetRawText(), d!.ModelType, CrJson.Options)!;
            var v = new PayloadValidator().Validate(templateId, model, allowLocalFilePaths: false);
            var warnings = v.Warnings.Concat(WarnOnSnakeCaseKeys(data)).Distinct(StringComparer.Ordinal).ToArray();
            LogValidateOutcome(templateId, v.Ok, v.Errors);
            return new JsonObject
            {
                ["ok"] = v.Ok,
                ["errors"] = ToArray(v.Errors),
                ["warnings"] = ToArray(warnings),
            };
        }
        catch (JsonException ex)
        {
            var errors = new[] { ex.Message };
            LogValidateOutcome(templateId, ok: false, errors);
            return new JsonObject { ["ok"] = false, ["errors"] = ToArray(errors) };
        }
    }

    [McpServerTool(Name = "render", Title = "Render template to PDF",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Render any template to PDF. Returns an artifact descriptor { filename, sha256, bytes, uri (file://), base64? }.")]
    public static async Task<JsonNode> Render(
        IDocumentRenderer renderer,
        [Description("Template id.")] string templateId,
        [Description("The document payload object for that template.")] JsonElement data,
        [Description("Density fit: auto | normal | compact | ultra. Default auto.")] string density = "auto",
        [Description("Include a bounded base64 copy of the PDF in the result.")] bool includeBase64 = true,
        CancellationToken ct = default)
    {
        var duration = Stopwatch.StartNew();
        var snakeCaseWarnings = WarnOnSnakeCaseKeys(data);
        var request = new RenderRequest
        {
            TemplateId = templateId,
            Json = data.GetRawText(),
            Options = ParseOptions(density),
            // The host renders for a local user, but never trust client-supplied local file paths over the wire.
            AllowLocalAttachmentPaths = false,
        };

        try
        {
            BrowserBootstrap.EnsureChromium();
            var result = await renderer.RenderAsync(request, ct).ConfigureAwait(false);
            var descriptor = ArtifactOutput.Write(result, templateId, includeBase64);
            var warnings = result.Warnings.Concat(snakeCaseWarnings).Distinct(StringComparer.Ordinal).ToArray();
            descriptor["warnings"] = ToArray(warnings);
            LogRenderSuccess(templateId, descriptor["filename"]?.GetValue<string>() ?? "<unknown>", duration.Elapsed);
            return descriptor;
        }
        catch (RenderValidationException ex)
        {
            LogRenderValidationFailed(templateId, ex.Errors);
            return new JsonObject { ["error"] = "validation_failed", ["details"] = ToArray(ex.Errors) };
        }
        catch (KeyNotFoundException ex)
        {
            LogRenderError(templateId, ex);
            return new JsonObject { ["error"] = ex.Message };
        }
        catch (Exception ex)
        {
            LogRenderError(templateId, ex);
            throw;
        }
    }

    [McpServerTool(Name = "install_browser", Title = "Install headless browser",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
    [Description("Manual fallback: download the headless Chromium shell if the bundled one is missing (idempotent). Normally unneeded — the shell ships inside the bundle, and renders fall back to system Edge/Chrome when it is broken. Honours PLAYWRIGHT_BROWSERS_PATH.")]
    public static string InstallBrowser()
    {
        var code = BrowserBootstrap.Install();
        return code == 0
            ? "Chromium is installed (or was already present)."
            : $"Chromium install exited with code {code}.";
    }

    private static RenderOptions ParseOptions(string density) => density.ToLowerInvariant() switch
    {
        "normal" => new RenderOptions { Fit = DensityFit.Fixed, Density = CollisionRenderer.Core.Density.Normal },
        "compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = CollisionRenderer.Core.Density.Compact },
        "ultra" or "ultra-compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = CollisionRenderer.Core.Density.UltraCompact },
        _ => new RenderOptions { Fit = DensityFit.Auto, Density = CollisionRenderer.Core.Density.Normal },
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

    private static IReadOnlyList<string> WarnOnSnakeCaseKeys(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddWarningsForObject(data, warnings, seen);

        if (data.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            AddWarningsForObject(meta, warnings, seen);
        }

        if (data.TryGetProperty("adverts", out var adverts) && adverts.ValueKind == JsonValueKind.Array)
        {
            foreach (var advert in adverts.EnumerateArray())
            {
                if (advert.ValueKind == JsonValueKind.Object)
                {
                    AddWarningsForObject(advert, warnings, seen);
                }
            }
        }

        return warnings;
    }

    private static void AddWarningsForObject(JsonElement obj, List<string> warnings, HashSet<string> seen)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (LooksSnakeCase(prop.Name) && seen.Add(prop.Name))
            {
                warnings.Add(
                    $"payload key '{prop.Name}' looks snake_case and will not bind to camelCase renderer models; "
                    + "use render_valuation_outputs for contract-shaped valuation payloads");
            }
        }
    }

    private static bool LooksSnakeCase(string key)
    {
        for (var i = 0; i < key.Length - 1; i++)
        {
            if (key[i] == '_' && key[i + 1] is >= 'a' and <= 'z')
            {
                return true;
            }
        }

        return false;
    }

    private static void LogValidateOutcome(string templateId, bool ok, IEnumerable<string> errors)
    {
        if (ok)
        {
            Console.Error.WriteLine($"[collisionrenderer-mcp] validate success: templateId={templateId}");
            return;
        }

        Console.Error.WriteLine(
            $"[collisionrenderer-mcp] validate validation_failed: templateId={templateId}; {string.Join(" | ", errors)}");
    }

    private static void LogRenderSuccess(string templateId, string filename, TimeSpan duration)
    {
        Console.Error.WriteLine(
            "[collisionrenderer-mcp] render success: "
            + $"templateId={templateId}; filename={filename}; duration_ms={(long)duration.TotalMilliseconds}");
    }

    private static void LogRenderValidationFailed(string templateId, IEnumerable<string> errors)
    {
        Console.Error.WriteLine(
            $"[collisionrenderer-mcp] render validation_failed: templateId={templateId}; {string.Join(" | ", errors)}");
    }

    private static void LogRenderError(string templateId, Exception ex)
    {
        Console.Error.WriteLine(
            $"[collisionrenderer-mcp] render renderer_error: templateId={templateId}; {ex.GetType().Name}: {ex.Message}");
    }
}
