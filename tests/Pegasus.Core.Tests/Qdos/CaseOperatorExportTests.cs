using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;

namespace Pegasus.Core.Tests.Qdos;

/// <summary>
/// CASE-019: an operator downloading their own case is not an EVA hand-off.
/// These tests hold both halves of that: the export carries a case the
/// hand-off would refuse, and the hand-off still refuses it.
/// </summary>
public sealed class CaseOperatorExportTests
{
    private static readonly DateOnly Today = new(2026, 8, 22);

    private static readonly EvaMappingAcceptance Accepted = new(
        CaseEvaMapping.MappingKey,
        CaseEvaMapping.MappingVersion,
        "docs/frd/frd-07-eva-and-external-engineering-handoff.md");

    [Fact]
    public void AFieldTheCaseDoesNotHoldIsExportedBlankAndNamed()
    {
        var export = CaseEvaMapping.MapForOperatorExport(Evidence(vatStatus: null), Accepted, Today);

        Assert.True(export.IsReady);
        Assert.Equal(["VAT Status"], export.UnrecordedFields);
        // Null in the record means "the case does not hold this"; the archive
        // writes it as an empty string, asserted below.
        Assert.Null(export.Source!.Fields.VatStatus);
        var vat = Assert.Single(export.Source.Provenance, field => field.Name == "VAT Status");
        Assert.Equal(EvaEvidenceStatus.Unrecorded, vat.Status);
    }

    [Fact]
    public void TheSameCaseIsStillRefusedAHandoff()
    {
        var mapping = CaseEvaMapping.MapForProduction(Evidence(vatStatus: null), Accepted);

        Assert.Null(mapping.Source);
        Assert.Contains(
            "VAT Status does not have accepted evidence.",
            mapping.BlockingReasons);
    }

    [Fact]
    public void AnAbsentInspectionDateBecomesTodayAndSaysSo()
    {
        var export = CaseEvaMapping.MapForOperatorExport(
            Evidence(inspectionDate: null),
            Accepted,
            Today);

        Assert.Equal("22/08/2026", export.Source!.Fields.InspectionDate);
        Assert.DoesNotContain("Inspection Date", export.UnrecordedFields);
        var inspection = Assert.Single(export.Source.Provenance, field => field.Name == "Inspection Date");
        Assert.Equal(CaseEvaMapping.ExportDateSource, inspection.Source);
    }

    [Fact]
    public void ASuggestedValueTravelsAsSuggestedRatherThanAccepted()
    {
        var export = CaseEvaMapping.MapForOperatorExport(
            Evidence(mileage: new("121823", EvaEvidenceStatus.Suggested, "vehicle-lookup", "latest-mot-observation/v2")),
            Accepted,
            Today);

        var mileage = Assert.Single(export.Source!.Provenance, field => field.Name == "Mileage");
        Assert.Equal(EvaEvidenceStatus.Suggested, mileage.Status);
        Assert.Equal("121823", mileage.Value);
    }

    [Fact]
    public void ASuggestedMileageStillCannotReachAHandoff()
    {
        var mapping = CaseEvaMapping.MapForProduction(
            Evidence(mileage: new("121823", EvaEvidenceStatus.Suggested, "vehicle-lookup", "latest-mot-observation/v2")),
            Accepted);

        Assert.Null(mapping.Source);
        Assert.Contains("Mileage does not have accepted evidence.", mapping.BlockingReasons);
    }

    [Fact]
    public void AnUnacceptedMappingRefusesTheExport()
    {
        var export = CaseEvaMapping.MapForOperatorExport(
            Evidence(),
            EvaMappingAcceptance.Unaccepted,
            Today);

        Assert.Null(export.Source);
        Assert.Equal([CaseEvaMapping.ActivationGateReason], export.BlockingReasons);
    }

    [Fact]
    public void TheArchiveCarriesAllThirteenKeysEvenWhenOneIsBlank()
    {
        var export = CaseEvaMapping.MapForOperatorExport(Evidence(vatStatus: null), Accepted, Today);

        var bundle = EvaBundleSchema.CreateOfflineReplay(
            export.Source!,
            new([Photograph()]));

        using var json = JsonDocument.Parse(bundle.JsonContent);
        var properties = json.RootElement.EnumerateObject().ToArray();
        Assert.Equal(13, properties.Length);
        Assert.Equal("VAT Status", properties[10].Name);
        Assert.Equal(JsonValueKind.String, properties[10].Value.ValueKind);
        Assert.Equal(string.Empty, properties[10].Value.GetString());
        Assert.Equal("EVA-QDOS26011.zip", bundle.FileName);

        // ENG-014: the export and the hand-off are one packaging, not two.
        // Whatever the hand-off ships, this ships -- the indented JSON and
        // Images/, with no companion file on either path.
        using var archive = new ZipArchive(new MemoryStream(bundle.Content), ZipArchiveMode.Read);
        Assert.Equal(
            ["EVA-QDOS26011.json", "Images/002 1_CLVoffside-V1.jpg"],
            archive.Entries.Select(entry => entry.FullName));
        Assert.StartsWith(
            "{\n  \"Work Provider\": ",
            Encoding.UTF8.GetString(bundle.JsonContent),
            StringComparison.Ordinal);
    }

    private static EvaAcceptedCaseEvidence Evidence(
        string? vatStatus = "No",
        string? inspectionDate = "03/05/2031",
        EvaEvidenceValue? mileage = null) =>
        new(
            Guid.Parse("266e5afa-5d66-4623-9136-abe21016df3b"),
            7,
            CaseAccepted: true,
            InstructionComplete: true,
            ImagesComplete: true,
            Value("QDOS26011"),
            Value("QDOS"),
            Value("ST66BCE"),
            Value("CX-5 SE-L D NAV"),
            Value("Mr Harry Sykes"),
            Value("19/08/2026"),
            Value("22/08/2026"),
            inspectionDate is null ? Missing() : Value(inspectionDate),
            new(EvaInspectionMode.ImageBasedAssessment, Value(CaseEvaMapping.ImageBasedAssessment)),
            Value("Rear-end collision on a slip road."),
            vatStatus is null ? Missing() : Value(vatStatus),
            mileage ?? Value("121823"),
            Value("miles"));

    private static EvaEvidenceValue Value(string value) =>
        new(value, EvaEvidenceStatus.Accepted, "accepted-case-data", "case-data/v1");

    private static EvaEvidenceValue Missing() =>
        new(null, EvaEvidenceStatus.Unrecorded, "unrecorded", "unrecorded");

    private static EvaBundleImage Photograph()
    {
        var content = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03 };
        return new(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            1,
            "1_CLVoffside-V1.jpg",
            "image/jpeg",
            DocumentSemanticRole.Image,
            DocumentSource.Intake,
            "case-custody:266e5afa:attachment:1",
            content,
            Convert.ToHexString(SHA256.HashData(content)),
            CustodyConfirmed: true,
            IsCurrent: true,
            2);
    }
}
