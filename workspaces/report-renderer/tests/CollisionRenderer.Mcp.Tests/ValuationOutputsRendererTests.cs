using System.Text.Json;
using System.Text.Json.Nodes;
using CollisionRenderer.Core;
using CollisionRenderer.Mcp.Valuation;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// Full-flow tests for the <c>render_valuation_outputs</c> drop-in, driving the real
/// document pipeline (catalog, Scriban composer, validator, capture append) through a
/// browser-free <see cref="StubPdfEngine"/>. Asserts the report-renderer-compatible
/// envelope and descriptor shape, and the preserved validation/capture behaviour.
/// </summary>
public class ValuationOutputsRendererTests
{
    private static readonly string CaptureBase64 = Convert.ToBase64String(StubPdfEngine.MakeOnePagePdf());

    [Fact]
    public async Task Happy_path_returns_two_artifacts_with_snake_case_descriptors()
    {
        var result = await Render(ValuationFixtures.Payload(), ValuationFixtures.Captures(CaptureBase64));

        Assert.True(result["validation"]!["ok"]!.GetValue<bool>(), result.ToJsonString());
        var artifacts = (JsonArray)result["artifacts"]!;
        Assert.Equal(2, artifacts.Count);

        Assert.Equal("valuation_report", artifacts[0]!["kind"]!.GetValue<string>());
        Assert.Equal("valuation_evidence_pack", artifacts[1]!["kind"]!.GetValue<string>());

        foreach (var artifact in artifacts)
        {
            // Descriptor keys must stay snake_case (byte-compatible with report-renderer).
            Assert.NotNull(artifact!["artifact_id"]);
            Assert.NotNull(artifact["sha256"]);
            Assert.Equal("application/pdf", artifact["media_type"]!.GetValue<string>());
            Assert.True(artifact["bytes"]!.GetValue<int>() > 0);
            var uri = artifact["uri"]!.GetValue<string>();
            Assert.StartsWith("file:", uri);
            Assert.True(File.Exists(new Uri(uri).LocalPath), $"artifact file should exist on disk: {uri}");
        }
    }

    [Fact]
    public async Task Evidence_pack_appends_one_page_per_capture()
    {
        // Stub report + pack table are each 1 page; the pack then appends 3 captured PDFs.
        var result = await Render(ValuationFixtures.Payload(), ValuationFixtures.Captures(CaptureBase64));

        var pack = (JsonObject)((JsonArray)result["artifacts"]!)[1]!;
        using var doc = PdfSharp.Pdf.IO.PdfReader.Open(
            new MemoryStream(Convert.FromBase64String(pack["base64"]!.GetValue<string>())),
            PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.Equal(4, doc.PageCount); // 1 table + 3 captures
    }

    [Fact]
    public async Task Numeric_money_and_mileage_fields_are_accepted()
    {
        // The published contract types money/mileage as number|string and the skill's worked
        // example sends bare numbers. Rewrite the fixture's money/mileage as JSON numbers and
        // prove the renderer no longer rejects them (regression: guide_value: 26417 threw
        // "The JSON value could not be converted to System.String. Path: $.guideValue").
        var payload = (JsonObject)JsonNode.Parse(ValuationFixtures.FullPayloadJson)!;
        payload["guide_value"] = 23900;
        payload["assessed_retail_value"] = 24750;
        ((JsonObject)payload["subject_vehicle"]!)["mileage"] = 31450;
        foreach (var advert in (JsonArray)payload["adverts"]!)
        {
            var a = (JsonObject)advert!;
            a["price"] = int.Parse(a["price"]!.GetValue<string>());
            a["mileage"] = int.Parse(a["mileage"]!.GetValue<string>());
            a["registration_year"] = int.Parse(a["registration_year"]!.GetValue<string>());
        }

        var result = await Render(JsonSerializer.SerializeToElement(payload), ValuationFixtures.Captures(CaptureBase64));

        Assert.True(result["validation"]!["ok"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Equal(2, ((JsonArray)result["artifacts"]!).Count);
    }

    [Fact]
    public async Task Preflight_missing_conclusion_returns_validation_error()
    {
        var payload = (JsonObject)JsonNode.Parse(ValuationFixtures.FullPayloadJson)!;
        payload.Remove("conclusion");

        var result = await Render(JsonSerializer.SerializeToElement(payload), ValuationFixtures.Captures(CaptureBase64));

        Assert.False(result["validation"]!["ok"]!.GetValue<bool>());
        Assert.Empty((JsonArray)result["artifacts"]!);
        var errors = ((JsonArray)result["validation"]!["errors"]!).Select(e => e!.GetValue<string>());
        Assert.Contains(errors, e => e.Contains("conclusion"));
    }


    [Fact]
    public async Task Missing_captures_fails_evidence_pack_completeness()
    {
        // Valid payload but NO captures: the pack requires a captured PDF per non-excluded advert.
        var result = await Render(ValuationFixtures.Payload(), captures: default);

        Assert.False(result["validation"]!["ok"]!.GetValue<bool>());
        var errors = ((JsonArray)result["validation"]!["errors"]!).Select(e => e!.GetValue<string>());
        Assert.Contains(errors, e => e.Contains("captured advert PDFs"));
    }

    [Fact]
    public async Task Raw_local_captured_pdf_path_is_rejected()
    {
        var payload = (JsonObject)JsonNode.Parse(ValuationFixtures.FullPayloadJson)!;
        var adverts = (JsonArray)payload["adverts"]!;
        ((JsonObject)adverts[0]!)["captured_pdf_path"] = "C:\\temp\\evil.pdf";

        var result = await Render(JsonSerializer.SerializeToElement(payload), ValuationFixtures.Captures(CaptureBase64));

        Assert.False(result["validation"]!["ok"]!.GetValue<bool>());
        var errors = ((JsonArray)result["validation"]!["errors"]!).Select(e => e!.GetValue<string>());
        Assert.Contains(errors, e => e.Contains("raw local file paths are not accepted"));
    }

    [Fact]
    public async Task Payload_passed_as_json_encoded_string_is_unwrapped()
    {
        // MCP hosts sometimes serialize the payload argument to a JSON STRING instead of
        // passing the object (observed live 2026-07-02 in Claude Desktop; the render then
        // failed preflight with "payload must be an object"). The renderer unwraps it.
        var payloadAsString = JsonSerializer.SerializeToElement(ValuationFixtures.FullPayloadJson);
        var capturesAsString = JsonSerializer.SerializeToElement(
            ValuationFixtures.Captures(CaptureBase64).GetRawText());

        var result = await Render(payloadAsString, capturesAsString);

        Assert.True(result["validation"]!["ok"]!.GetValue<bool>(), result.ToJsonString());
        Assert.Equal(2, ((JsonArray)result["artifacts"]!).Count);
    }

    [Fact]
    public async Task Non_json_string_payload_still_fails_preflight()
    {
        var result = await Render(
            JsonSerializer.SerializeToElement("not json at all"),
            ValuationFixtures.Captures(CaptureBase64));

        Assert.False(result["validation"]!["ok"]!.GetValue<bool>());
        var errors = ((JsonArray)result["validation"]!["errors"]!).Select(e => e!.GetValue<string>());
        Assert.Contains(errors, e => e.Contains("payload must be an object"));
    }

    private static async Task<JsonObject> Render(JsonElement payload, JsonElement captures)
    {
        await using var renderer = CollisionRendererFactory.CreateRenderer(new StubPdfEngine());
        return await new ValuationOutputsRenderer(renderer).RenderAsync(payload, captures, includeBase64: true);
    }
}
