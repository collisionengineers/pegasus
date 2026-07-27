using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CollisionRenderer.Mcp.Valuation;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// Cross-repo drift guards. The <c>evidence_pack_payload</c> contract (valuation/v1) lives in
/// the valuation-adverts-connector repo and is hand-mirrored here in C# (ValuationPolicyValidator
/// + the snake→camel mapper). Nothing enforces that the mirror stays in step with the contract —
/// which is exactly how <c>meta.report_date</c> came to be silently dropped. These tests fail when
/// the mirror drifts.
/// </summary>
public class ContractConformanceTests
{
    [Fact]
    public void Validator_required_lists_match_the_contract_schema()
    {
        var schemaPath = FindContractSchema("evidence-pack-payload.schema.json");
        if (schemaPath is null)
        {
            // Renderer built standalone (no collisionsuite super-checkout): the contract lives in
            // a sibling repo, so there is nothing to compare against here — skip, don't fail.
            return;
        }

        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var defs = schema.RootElement.GetProperty("$defs");

        Assert.Equal(new[] { "your_ref" }, RequiredOf(defs, "Meta"));
        Assert.Equal(RequiredOf(defs, "SubjectVehicle"), Sorted(ValuationPolicyValidator.RequiredSubject));
        Assert.Equal(RequiredOf(defs, "EvidenceAdvert"), Sorted(ValuationPolicyValidator.RequiredAdvert));
    }

    [Fact]
    public void Contract_types_money_and_mileage_as_number_or_string()
    {
        // Guard the DF73VSA regression at its source: the shared contract keeps money/mileage as
        // number|string, and the renderer accepts both (LenientStringConverter — see
        // LenientStringConverterTests). Do NOT "fix" any future drift by forcing strings into the
        // contract; the renderer is the party that must adapt.
        var schemaPath = FindContractSchema("evidence-pack-payload.schema.json");
        if (schemaPath is null)
        {
            return; // standalone build — sibling contract absent
        }

        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = schema.RootElement;
        var props = root.GetProperty("properties");
        var defs = root.GetProperty("$defs");
        var subject = defs.GetProperty("SubjectVehicle").GetProperty("properties");
        var advert = defs.GetProperty("EvidenceAdvert").GetProperty("properties");

        AssertNumberOrString(props, "guide_value");
        AssertNumberOrString(props, "assessed_retail_value");
        AssertNumberOrString(subject, "mileage");
        AssertNumberOrString(advert, "price");
        AssertNumberOrString(advert, "mileage");
        AssertNumberOrString(advert, "registration_year");
    }

    [Fact]
    public void ToReportJson_aliases_meta_report_date_to_date()
    {
        using var payload = JsonDocument.Parse(
            "{\"meta\":{\"your_ref\":\"KLZ-2025-184\",\"report_date\":\"15/03/2026\"}}");

        var json = ValuationPayloadMapper.ToReportJson(payload.RootElement);
        using var doc = JsonDocument.Parse(json);
        var meta = doc.RootElement.GetProperty("meta");

        Assert.Equal("15/03/2026", meta.GetProperty("date").GetString());
        Assert.False(
            meta.TryGetProperty("reportDate", out _),
            "meta.report_date must be aliased to meta.date, not left as reportDate");
    }

    [Fact]
    public void NormalizeUrl_matches_across_cosmetic_drift()
    {
        // A trailing slash, host casing, an explicit default port and a #fragment must NOT
        // stop a captured PDF from matching its advert (#3 — capture↔advert url matching).
        var canonical = ValuationPayloadMapper.NormalizeUrl("https://www.autotrader.co.uk/car-details/123");
        Assert.Equal(canonical, ValuationPayloadMapper.NormalizeUrl("https://www.AutoTrader.co.uk/car-details/123/"));
        Assert.Equal(canonical, ValuationPayloadMapper.NormalizeUrl("https://www.autotrader.co.uk:443/car-details/123#gallery"));
    }

    private static void AssertNumberOrString(JsonElement props, string field)
    {
        var types = props.GetProperty(field).GetProperty("type").EnumerateArray().Select(e => e.GetString()!);
        // Sorted Ordinal: "number" < "string".
        Assert.Equal(new[] { "number", "string" }, Sorted(types));
    }

    private static string[] RequiredOf(JsonElement defs, string defName) =>
        Sorted(defs.GetProperty(defName).GetProperty("required").EnumerateArray().Select(e => e.GetString()!));

    private static string[] Sorted(IEnumerable<string> items) =>
        items.OrderBy(s => s, StringComparer.Ordinal).ToArray();

    private static string? FindContractSchema(string fileName)
    {
        var rel = Path.Combine(
            "connectors", "valuation-adverts-connector", "contracts", "schemas", "valuation", "v1", fileName);
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, rel);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
