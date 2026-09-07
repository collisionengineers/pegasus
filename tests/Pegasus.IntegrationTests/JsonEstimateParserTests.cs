using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Assessment;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-026: the Pegasus-owned JSON estimate document beside the Audatex
/// parser. Money and hours are read exactly; anything ambiguous rejects the
/// whole import.
/// </summary>
public sealed class JsonEstimateParserTests
{
    private readonly JsonEstimateParser parser = new();

    [Theory]
    [InlineData("estimate.json", "application/octet-stream", true)]
    [InlineData("ESTIMATE.JSON", "", true)]
    [InlineData("estimate.txt", "application/json", true)]
    [InlineData("estimate.pdf", "application/pdf", false)]
    public void CanParseRecognizesJsonByNameOrMediaType(string fileName, string mediaType, bool expected) =>
        Assert.Equal(expected, parser.CanParse(fileName, mediaType));

    [Fact]
    public void TheRouteIsJson() =>
        Assert.Equal(RepairSpecificationSourceRoute.Json, parser.Route);

    [Fact]
    public void ParsesOperationsAndTypesWithTheirOwnFigures()
    {
        var result = parser.Parse(Bytes(
            """
            {
              "schema": "pegasus-estimate/1",
              "sourceVersion": "repairer-estimate-42",
              "lines": [
                { "operation": "Replace", "description": "Front bumper", "partNumber": "51 11 8 067", "quantity": 2, "price": 620.20 },
                { "operation": "Repair", "description": "Repair wing", "labourHours": 2.5 },
                { "operation": "Paint", "description": "Paint wing", "paintHours": 1.5 },
                { "type": "check_labour", "description": "Geometry check", "labourHours": 0.5 },
                { "operation": "Replace", "description": "Headlamp (price to follow)" }
              ]
            }
            """));

        Assert.Equal("repairer-estimate-42", result.SourceVersion);
        Assert.Equal(5, result.Lines.Count);

        var bumper = result.Lines[0];
        Assert.Equal("new_part", bumper.Type);
        Assert.Equal("51 11 8 067", bumper.PartNumber);
        Assert.Equal(2, bumper.Quantity);
        Assert.Equal(620.20m, bumper.Price);
        Assert.False(bumper.Unpriced);
        Assert.Equal("estimated", bumper.Status);
        Assert.Equal("reference", bumper.EvidenceLabel);

        Assert.Equal("repair", result.Lines[1].Type);
        Assert.Equal(2.5m, result.Lines[1].WorkUnits);
        Assert.Equal("paint_repair", result.Lines[2].Type);
        Assert.Equal(1.5m, result.Lines[2].PaintWorkUnits);
        Assert.Equal("check_labour", result.Lines[3].Type);

        var headlamp = result.Lines[4];
        Assert.Null(headlamp.Price);
        Assert.True(headlamp.Unpriced);

        // Every parsed line passes the one line normaliser unchanged.
        Assert.Equal(5, AssessmentPolicy.NormalizeRepairSpecificationLines(result.Lines).Count);
    }

    [Fact]
    public void ParsesBlendSpecialistRowMaterialsProvenanceAndTheDocumentsOwnTotals()
    {
        var result = parser.Parse(Bytes(
            """
            {
              "schema": "pegasus-estimate/1",
              "sourceVersion": "repairer-estimate-43",
              "provider": "Repairer",
              "totals": { "parts": 299.80, "panelWorkUnits": 4.7, "materials": 75.60, "gross": 1142.74 },
              "lines": [
                { "operation": "Blend", "description": "Blend adjacent panel", "paintHours": 0.8, "rowId": "L-7" },
                { "operation": "Specialist", "description": "ADAS calibration", "price": 180.00, "labourHours": 6 },
                { "operation": "Repair", "description": "Repair wing", "labourHours": 0.333333, "materials": 45.60 }
              ]
            }
            """));

        Assert.Equal("Repairer", result.ProviderName);

        var blend = result.Lines[0];
        Assert.Equal("paint_blend", blend.Type);
        Assert.Equal(0.8m, blend.PaintWorkUnits);
        Assert.Equal("L-7", blend.SourceRowIdentity);

        var specialist = result.Lines[1];
        Assert.Equal("specialist_fixed", specialist.Type);
        Assert.Equal(180.00m, specialist.Price);
        Assert.Equal(6m, specialist.WorkUnits);

        var repair = result.Lines[2];
        // The provider's own time survives: it is not rounded to 0.3.
        Assert.Equal(0.333333m, repair.WorkUnits);
        Assert.Equal(45.60m, repair.Materials);
        Assert.Null(repair.SourceRowIdentity);

        // The document's own totals are evidence beside the calculation.
        var totals = Assert.IsType<EstimateSourceTotals>(result.SourceTotals);
        Assert.Equal(299.80m, totals.Parts);
        Assert.Equal(4.7m, totals.PanelWorkUnits);
        Assert.Equal(75.60m, totals.Materials);
        Assert.Equal(1_142.74m, totals.Gross);
        Assert.Null(totals.Net);

        Assert.Equal(3, AssessmentPolicy.NormalizeRepairSpecificationLines(result.Lines).Count);
    }

    [Fact]
    public void ADocumentThatNamesNoProviderOrTotalsStillImports()
    {
        var result = parser.Parse(Bytes(
            """
            {
              "schema": "pegasus-estimate/1",
              "sourceVersion": "v9",
              "lines": [ { "operation": "Repair", "description": "Repair wing", "labourHours": 1.25 } ]
            }
            """));

        Assert.Equal(JsonEstimateParser.DefaultProviderName, result.ProviderName);
        Assert.Null(result.SourceTotals);
        Assert.Equal(1.25m, Assert.Single(result.Lines).WorkUnits);
    }

    // The reason leads each case: xUnit truncates long arguments in the
    // display name, so the shared JSON prefix alone would collapse these
    // cases to identical names and break the test-shard partitioner.
    [Theory]
    [InlineData("not json", "{ not json")]
    [InlineData("wrong schema", """{ "schema": "other/1", "sourceVersion": "v", "lines": [ { "operation": "Repair", "description": "x" } ] }""")]
    [InlineData("missing source version", """{ "schema": "pegasus-estimate/1", "lines": [ { "operation": "Repair", "description": "x" } ] }""")]
    [InlineData("empty lines", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [] }""")]
    [InlineData("unknown operation", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Weld", "description": "x" } ] }""")]
    [InlineData("unknown type", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "type": "weld", "description": "x" } ] }""")]
    [InlineData("missing description", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Repair" } ] }""")]
    [InlineData("negative price", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Replace", "description": "x", "price": -1 } ] }""")]
    [InlineData("sub-cent price", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Replace", "description": "x", "price": 1.005 } ] }""")]
    [InlineData("labour beyond the persisted precision", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Repair", "description": "x", "labourHours": 1.2345678 } ] }""")]
    [InlineData("labour beyond the hour bound", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Repair", "description": "x", "labourHours": 1001 } ] }""")]
    [InlineData("sub-cent row materials", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Repair", "description": "x", "materials": 1.005 } ] }""")]
    [InlineData("unreadable printed total", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "totals": { "gross": -1 }, "lines": [ { "operation": "Repair", "description": "x" } ] }""")]
    [InlineData("zero quantity", """{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Replace", "description": "x", "quantity": 0 } ] }""")]
    public void AnythingAmbiguousRejectsTheWholeImport(string reason, string document)
    {
        var rejected = Record.Exception(() => parser.Parse(Bytes(document)));
        Assert.True(rejected is EstimateParseRejectedException, reason);
    }

    private static ReadOnlyMemory<byte> Bytes(string document) => Encoding.UTF8.GetBytes(document);
}
