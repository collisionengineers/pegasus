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

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "schema": "other/1", "sourceVersion": "v", "lines": [ { "operation": "Repair", "description": "x" } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "lines": [ { "operation": "Repair", "description": "x" } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Weld", "description": "x" } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "type": "weld", "description": "x" } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Repair" } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Replace", "description": "x", "price": -1 } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Replace", "description": "x", "price": 1.005 } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Repair", "description": "x", "labourHours": 1.25 } ] }""")]
    [InlineData("""{ "schema": "pegasus-estimate/1", "sourceVersion": "v", "lines": [ { "operation": "Replace", "description": "x", "quantity": 0 } ] }""")]
    public void AnythingAmbiguousRejectsTheWholeImport(string document) =>
        Assert.Throws<EstimateParseRejectedException>(() => parser.Parse(Bytes(document)));

    private static ReadOnlyMemory<byte> Bytes(string document) => Encoding.UTF8.GetBytes(document);
}
