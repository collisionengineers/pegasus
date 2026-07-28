using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Mcp.Tools;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

public class RenderToolsTests
{
    private const string TemplateId = "market-valuation-evidence";

    [Fact]
    public void Validate_warns_on_snake_case_payload_keys()
    {
        var payload = SamplePayload();
        ((JsonObject)payload["meta"]!)["report_date"] = "20/06/2026";

        var result = (JsonObject)RenderTools.Validate(TemplateId, Data(payload));

        var warnings = Warnings(result);
        Assert.Contains(warnings, w => w.Contains("payload key 'report_date' looks snake_case"));
    }

    [Fact]
    public void Validate_does_not_warn_on_camel_case_payload_keys()
    {
        var result = (JsonObject)RenderTools.Validate(TemplateId, Data(SamplePayload()));

        var warnings = Warnings(result);
        Assert.DoesNotContain(warnings, w => w.Contains("looks snake_case"));
    }

    [Fact]
    public async Task Render_appends_snake_case_payload_warnings()
    {
        var payload = SamplePayload();
        ((JsonObject)payload["meta"]!)["report_date"] = "20/06/2026";

        await using var renderer = CollisionRendererFactory.CreateRenderer(new StubPdfEngine());
        var result = (JsonObject)await RenderTools.Render(renderer, TemplateId, Data(payload), includeBase64: false);

        var warnings = Warnings(result);
        Assert.Contains(warnings, w => w.Contains("payload key 'report_date' looks snake_case"));
    }

    private static JsonObject SamplePayload() =>
        (JsonObject)JsonNode.Parse(CollisionRendererFactory.AuthoringCatalog.GetStarterJson(TemplateId))!;

    private static JsonElement Data(JsonNode payload) =>
        JsonSerializer.SerializeToElement(payload);

    private static string[] Warnings(JsonObject result) =>
        ((JsonArray)result["warnings"]!).Select(w => w!.GetValue<string>()).ToArray();
}
