using System.Text.Json;
using CollisionRenderer.Core;
using CollisionRenderer.Core.Models;
using CollisionRenderer.Mcp.Valuation;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// The dropped-field guard. A silent failure to carry a snake_case field onto its
/// camelCase model property produces a plausible-but-wrong PDF, so this asserts that a
/// fully-populated payload lands on <i>every</i> model property — and that the structural
/// <c>subject_vehicle → subject</c> rename happens.
/// </summary>
public class ValuationPayloadMapperTests
{
    [Theory]
    [InlineData("body_type", "bodyType")]
    [InlineData("first_registered", "firstRegistered")]
    [InlineData("supports_assessed_value", "supportsAssessedValue")]
    [InlineData("derivative_or_engine", "derivativeOrEngine")]
    [InlineData("sufficient_for_pdf", "sufficientForPdf")]
    [InlineData("url", "url")]
    [InlineData("vin", "vin")]
    public void ToCamelCase_renames_snake_keys(string snake, string camel) =>
        Assert.Equal(camel, ValuationPayloadMapper.ToCamelCase(snake));

    [Fact]
    public void ToReportJson_renames_subject_vehicle_to_subject()
    {
        var json = ValuationPayloadMapper.ToReportJson(ValuationFixtures.Payload());
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("subject", out _), "expected renamed 'subject' key");
        Assert.False(doc.RootElement.TryGetProperty("subjectVehicle", out _), "stale 'subjectVehicle' key must not survive");
        Assert.False(doc.RootElement.TryGetProperty("subject_vehicle", out _), "snake 'subject_vehicle' key must not survive");
    }

    [Fact]
    public void ToReportJson_populates_every_subject_field()
    {
        var doc = Deserialize(ValuationPayloadMapper.ToReportJson(ValuationFixtures.Payload()));
        var s = doc.Subject;

        Assert.Equal("AB12 CDE", s.Registration);
        Assert.Equal("BMW", s.Make);
        Assert.Equal("3 Series", s.Model);
        Assert.Equal("320d M Sport", s.Derivative);
        Assert.Equal("BMW 3 Series 320d M Sport Saloon", s.VehicleDescription);
        Assert.Equal("Saloon", s.BodyType);
        Assert.Equal("Diesel", s.Fuel);
        Assert.Equal("Automatic", s.Transmission);
        Assert.Equal("1995cc", s.Engine);
        Assert.Equal("01/03/2022", s.FirstRegistered);
        Assert.Equal("31450", s.Mileage);
        Assert.Equal("Mineral Grey Metallic", s.Colour);
        Assert.Equal("No adverse history recorded", s.VehicleHistory);
        Assert.Equal("WBA00000000000000", s.Vin);
    }

    [Fact]
    public void ToReportJson_populates_top_level_and_evidence_assessment()
    {
        var doc = Deserialize(ValuationPayloadMapper.ToReportJson(ValuationFixtures.Payload()));

        Assert.Equal("PCH25309", doc.Meta.OurRef);
        Assert.Equal("KLZ-2025-184", doc.Meta.YourRef);
        Assert.Equal("guide_supported", doc.ValuationMode);
        Assert.Equal("23900", doc.GuideValue);
        Assert.Equal("24750", doc.AssessedRetailValue);
        Assert.False(string.IsNullOrWhiteSpace(doc.MarketResearch));
        Assert.False(string.IsNullOrWhiteSpace(doc.Conclusion));
        Assert.Equal("All figures are stated inclusive of VAT where applicable.", doc.VatNote);
        Assert.False(doc.IsCommercialVehicle);
        Assert.Equal(3, doc.ValuationCommentary.Count);
        Assert.NotNull(doc.EvidenceAssessment);
        Assert.True(doc.EvidenceAssessment!.SufficientForPdf);
        Assert.False(string.IsNullOrWhiteSpace(doc.EvidenceAssessment.Basis));
    }

    [Fact]
    public void ToReportJson_populates_every_advert_field()
    {
        var doc = Deserialize(ValuationPayloadMapper.ToReportJson(ValuationFixtures.Payload()));
        Assert.Equal(3, doc.Adverts.Count);

        var a = doc.Adverts[0];
        Assert.Equal("Retail listing", a.Source);
        Assert.Equal("https://example.com/advert/100231", a.Url);
        Assert.Equal("25495", a.Price);
        Assert.Equal("BMW", a.Make);
        Assert.Equal("3 Series", a.Model);
        Assert.Equal("320d M Sport", a.DerivativeOrEngine);
        Assert.Equal("2022", a.RegistrationYear);
        Assert.Equal("28000", a.Mileage);
        Assert.Equal("Diesel", a.Fuel);
        Assert.Equal("Automatic", a.Transmission);
        Assert.Equal("Saloon", a.BodyStyle);
        Assert.Equal("Franchise", a.SellerType);
        Assert.Equal("Manchester", a.Location);
        Assert.Equal("20/06/2026", a.DateAccessed);
        Assert.Equal("Closely comparable specification and age.", a.ComparabilityNote);
        Assert.Equal("Slightly lower mileage than the subject vehicle.", a.DifferencesNote);
        Assert.Equal(true, a.SupportsAssessedValue);
        Assert.Equal("supportive", a.EvidenceRole);
        Assert.Equal(true, a.IsMateriallyComparable);
        Assert.Equal("A-100231", a.AdvertId);
        Assert.Equal("shots/100231.png", a.ScreenshotPath);
        Assert.Equal("Directly supportive of the assessed retail value.", a.ReportComment);
        Assert.Equal("Inc. VAT", a.VatStatus);
        Assert.Equal("0", a.AdminFee);
        Assert.Equal("0", a.DeliveryFee);
    }

    private static MarketValuationEvidenceDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<MarketValuationEvidenceDocument>(json, CrJson.Options)!;
}
