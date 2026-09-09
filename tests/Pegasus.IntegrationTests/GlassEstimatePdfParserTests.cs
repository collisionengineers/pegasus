using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Assessment;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Immutable originals and independent full-row transcriptions, never parser
/// output as its own oracle. The existing reference-pack opt-in owns access.
/// </summary>
[Trait("Category", "Corpus")]
public sealed class GlassEstimatePdfParserTests
{
    [Fact]
    public void CompetingPrintedProvidersRejectWithoutQualifyingOcr()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Glass's Information Services", 9, new PdfPoint(28, 700), font);
        page.AddText("Audatex System", 9, new PdfPoint(28, 680), font);
        var refusal = Assert.Throws<EstimateParseRejectedException>(() => new PdfEstimateDocumentParser().Parse(builder.Build()));
        Assert.Contains("exactly one supported estimate provider", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableAmountRefusesTheWholeGlassTable()
    {
        // A structural corruption of the supplied VX21TZD B01: its printed
        // numeric cell cannot be rescued by a format or OCR fallback.
        PdfEstimateDocumentParser.VisualRow[] rows =
        [
            new(1, 700, [new(28, "Body"), new(350, "Overlap-time")], "Body Overlap-time"),
            new(1, 680, [new(28, "88995001"), new(92, "Roof Drip Moulding (R)"), new(247, "UI"),
                new(318, "0.OO"), new(382, "0.20"), new(520, "0.00")], "88995001 Roof Drip Moulding (R) UI 0.OO 0.20 0.00")
        ];
        var refusal = Assert.Throws<EstimateParseRejectedException>(() => GlassEstimatePdfParser.Parse(rows));
        Assert.Contains("printed amount is unreadable", refusal.Message, StringComparison.Ordinal);
    }

    [ReferencePackTheory]
    [InlineData("VX21TZD", "1046012231790__VX21TZD calculation sheet.pdf", "c75b94438ad6a57aae8b6edb8de554498920c1626cb0c8b0046a3322a55c1016", 10134,
        "AD5107957FDAEE562C29C84D939A709812A4F8A9FC21A057306768FDD387777B", 30, 3, "990.15", "198.03", "1188.18")]
    [InlineData("LT72PYX", "1313339771083__LT72PYX Calculation....pdf", "3bc7244f310be857b82ff87c7c2e3de3c23ff8f43a5109599b1d5f77b0bdd09d", 16490,
        "FBAF36331DCB2ADA7AB4FA0153B0E5F05636DAE8266190D39D49E5626954782D", 102, 45, "4461.56", "892.31", "5353.87")]
    [InlineData("ML23OXR", "1710254173321__Calculation ML23 OXR.pdf", "a0a56cc291b93c5a28c9ef575b98c65aee759fd9bedfd811a1463ebe6e0817f8", 11763,
        "AE3609C9822924FCAE41E9E7ABDA15AD3DC80B65A8BF5F31AA9A5A8D399B2CB6", 46, 8, "5092.59", "1018.52", "6111.11")]
    [InlineData("LG73ZCJ", "2228602993671__CalculationPDF.pdf", "ab3472cd160a08f19251439c02966f747753051a1204db97a2465d505a0bb45f", 12107,
        "07934D40D3947FFF0B5AD81238D4E761A27C0AE88EE8E1670F07F39480002F99", 66, 9, "3063.03", "612.61", "3675.64")]
    public async Task EveryReadableOriginalMatchesItsIndependentFullOrderedRowOracle(
        string identity, string fileName, string hash, int length, string oracleHash,
        int rowCount, int partCount, string net, string vat, string gross)
    {
        var root = Top15InstructionCorpusTests.PackRoot();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, "glasses-integration", "glass_ref_docs", fileName));
        Assert.Equal(length, bytes.Length);
        Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        var oracleBytes = await File.ReadAllBytesAsync(Path.Combine(root, "current", $"glass-row-oracle-{identity}.md"));
        Assert.Equal(oracleHash, Convert.ToHexString(SHA256.HashData(oracleBytes)));
        var tables = Encoding.UTF8.GetString(oracleBytes).Split('\n')
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries)[1..^1]).ToArray();
        var expected = tables.Where(cells => cells.Length == 11 && cells[0].Length == 3
            && "BAP".Contains(cells[0][0]) && char.IsAsciiDigit(cells[0][1])).ToArray();

        var parsed = new PdfEstimateDocumentParser().Parse(bytes);
        Assert.Equal(RepairSpecificationSourceRoute.Glasses, parsed.Route);
        Assert.Equal("Glass's", parsed.ProviderName);
        Assert.Contains(identity, parsed.SourceVersion.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(rowCount, expected.Length);
        Assert.Equal(rowCount, parsed.Lines.Count);
        var bySource = new Dictionary<string, EstimateLineInput>(StringComparer.Ordinal);
        for (var index = 0; index < expected.Length; index++)
        {
            var source = expected[index]; var actual = parsed.Lines[index];
            var section = source[0][0] switch { 'B' => "body", 'A' => "auxiliary", _ => "paint" };
            var ordinal = int.Parse(source[0].AsSpan(1), CultureInfo.InvariantCulture);
            Assert.Equal($"{section}:p{source[1]}:r{ordinal}:{Blank(source[3])}", actual.SourceRowIdentity);
            Assert.Equal(source[5], actual.Description);
            Assert.Equal(Blank(source[3]), actual.GuideCode);
            Assert.Equal(Number(source[6]), section == "paint" ? actual.PaintWorkUnits : actual.WorkUnits);
            Assert.Equal(Number(source[9]), source[4] == "RP" ? actual.Price : actual.Materials);
            if (source[2] == "included")
            {
                Assert.Null(actual.Price);
                Assert.Null(actual.WorkUnits);
                Assert.Null(actual.Materials);
                Assert.Contains("Included in", actual.Justification, StringComparison.Ordinal);
            }
            if (source[4] != "-")
                Assert.Contains($": {source[4]}.", actual.Justification, StringComparison.Ordinal);
            if (Number(source[7]) is { } overlap)
                Assert.Contains(FormattableString.Invariant($"Printed overlap: {overlap:0.00} h"), actual.Justification, StringComparison.Ordinal);
            if (Number(source[8]) is { } labour)
                Assert.Contains(FormattableString.Invariant($"Printed labour: {labour:0.00} GBP"), actual.Justification, StringComparison.Ordinal);
            bySource.Add(source[0], actual);
        }
        var appendix = tables.Where(cells => cells.Length == 5 && cells[0].Length == 3 && cells[0][0] == 'T'
            && char.IsAsciiDigit(cells[0][1])).ToArray();
        Assert.Equal(partCount, appendix.Length);
        Assert.Equal(partCount, parsed.Lines.Count(line => line.PartNumber is not null));
        foreach (var part in appendix)
        {
            var actual = bySource[part[4][..3]];
            Assert.Equal(part[2], actual.PartNumber);
            Assert.Equal(Number(part[3]), actual.Price);
        }
        Assert.Equal(Number(net), parsed.SourceTotals!.Net);
        Assert.Equal(Number(vat), parsed.SourceTotals.Vat);
        Assert.Equal(Number(gross), parsed.SourceTotals.Gross);
        Assert.Equal(partCount, parsed.Lines.Count(line => line.Type == "new_part"));
        Assert.Equal(parsed.Lines.Count, AssessmentPolicy.NormalizeRepairSpecificationLines(parsed.Lines).Count);
        // Use the existing Core money owner, at the source's stated common
        // section rate, to prove chargeable EC time and no appendix double cost.
        // This is reconciliation evidence, not a chosen production rate card.
        var rate = Number(tables.Single(cells => cells.Length == 7 && cells[0] == "Body")[2]);
        var recorded = parsed.Lines.Select((line, index) => new CaseEstimateLineRecord(
            Guid.NewGuid(), index + 1, line.Type, line.GuideCode, line.Description, line.WorkUnits,
            line.Price, line.Unpriced, line.PartNumber, line.Betterment, line.Status, line.EvidenceLabel,
            line.Justification, ActorKind.Automation, "oracle-check", DateTimeOffset.UnixEpoch, null, null,
            line.PaintWorkUnits, line.Quantity, line.Materials)).ToArray();
        var calculation = EstimateTotals.Compute(new(Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(parsed.Route, "oracle-source", parsed.SourceVersion, hash), recorded, null, "oracle-check",
            DateTimeOffset.UnixEpoch, null, null, null, null,
            new("Oracle reconciliation", null, rate, null, null, 20m, null,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered))));
        Assert.Equal(Number(net), calculation.Printed.Net);
        Assert.Equal(Number(gross), calculation.Printed.Gross);
        if (identity == "VX21TZD")
        {
            Assert.Equal("88995001", bySource["A02"].GuideCode);
            Assert.Null(bySource["A02"].PartNumber);
            Assert.Contains("60301801", bySource["A02"].Justification, StringComparison.Ordinal);
        }
        if (identity == "LT72PYX")
        {
            Assert.Equal("check_labour", bySource["B59"].Type);
            Assert.Equal(2, parsed.Lines.Count(line => string.Equals(line.Description, "Rear camera reset", StringComparison.OrdinalIgnoreCase)));
        }
        if (identity == "LG73ZCJ")
            Assert.Equal("Panel repair sundries (including body fi", bySource["A40"].Description);
    }

    private static string? Blank(string value) => value == "-" ? null : value;
    private static decimal? Number(string value) => value == "-" ? null
        : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}
