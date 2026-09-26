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

    [Fact]
    public void SectionLabourUsesThePrintedHoursAndRateBeforeRounding()
    {
        var parsed = GlassEstimatePdfParser.Parse(RoundingDocument("66.62"));

        Assert.Equal(7, parsed.Lines.Count);
        Assert.Equal(557.98m, parsed.SourceTotals!.Net);
    }

    [Fact]
    public void AOnePennyRowLabourDisagreementStillRefusesTheWholeGlassTable()
    {
        var refusal = Assert.Throws<EstimateParseRejectedException>(
            () => GlassEstimatePdfParser.Parse(RoundingDocument("66.63")));

        Assert.Contains("main rows disagree", refusal.Message, StringComparison.Ordinal);
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
            line.Price, line.Unpriced, line.PartNumber, line.Betterment, line.EvidenceLabel,
            line.Justification, ActorKind.Automation, "oracle-check", DateTimeOffset.UnixEpoch,
            line.PaintWorkUnits, line.Quantity, line.Materials)).ToArray();
        var calculation = EstimateTotals.Compute(new(Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
            new(parsed.Route, "oracle-source", parsed.SourceVersion, hash), recorded, "oracle-check",
            DateTimeOffset.UnixEpoch,
            new("Oracle reconciliation", rate, null, 20m,
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

    private static PdfEstimateDocumentParser.VisualRow[] RoundingDocument(string firstPaintLabour) =>
    [
        new(1, 760, [new(28, "Vehicle Registration Number:"), new(200, "AB12 CDE")], "Vehicle Registration Number: AB12 CDE"),
        new(1, 750, [new(28, "VIN:"), new(200, "TESTVIN1234567890")], "VIN: TESTVIN1234567890"),
        new(1, 740, [new(28, "Date:"), new(200, "16/09/2026")], "Date: 16/09/2026"),
        new(1, 730, [new(28, "Database version:"), new(200, "TEST")], "Database version: TEST"),
        new(1, 720, [new(28, "Labour time unit:"), new(200, "1 Hour")], "Labour time unit: 1 Hour"),
        new(1, 710, [new(28, "Currency:"), new(200, "GBP")], "Currency: GBP"),

        new(1, 690, [new(28, "Body"), new(350, "Overlap-time")], "Body Overlap-time"),
        new(1, 680, [new(28, "1001"), new(100, "Body panel"), new(240, "R"), new(300, "1.00"),
            new(450, "83.28"), new(520, "0.00")], "1001 Body panel R 1.00 83.28 0.00"),
        new(1, 670, [new(28, "Labour costs"), new(520, "83.28")], "Labour costs 83.28"),
        new(1, 660, [new(28, "Material costs"), new(520, "0.00")], "Material costs 0.00"),
        new(1, 650, [new(28, "Total"), new(520, "83.28")], "Total 83.28"),

        new(1, 630, [new(28, "Auxiliary work"), new(350, "Overlap-time")], "Auxiliary work Overlap-time"),
        new(1, 620, [new(28, "2001"), new(100, "Auxiliary operation"), new(240, "R"), new(300, "1.00"),
            new(450, "83.28"), new(520, "0.00")], "2001 Auxiliary operation R 1.00 83.28 0.00"),
        new(1, 610, [new(28, "Labour costs"), new(520, "83.28")], "Labour costs 83.28"),
        new(1, 600, [new(28, "Material costs"), new(520, "0.00")], "Material costs 0.00"),
        new(1, 590, [new(28, "Total"), new(520, "83.28")], "Total 83.28"),

        new(1, 570, [new(28, "Paint"), new(100, "Paint type")], "Paint Paint type"),
        new(1, 560, [new(28, "Paint row 1"), new(240, "200"), new(310, "I"), new(360, "0.80"),
            new(430, firstPaintLabour), new(520, "0.00")], $"Paint row 1 200 I 0.80 {firstPaintLabour} 0.00"),
        new(1, 550, [new(28, "Paint row 2"), new(240, "200"), new(310, "I"), new(360, "1.60"),
            new(430, "133.25"), new(520, "0.00")], "Paint row 2 200 I 1.60 133.25 0.00"),
        new(1, 540, [new(28, "Paint row 3"), new(240, "200"), new(310, "I"), new(360, "1.70"),
            new(430, "141.58"), new(520, "0.00")], "Paint row 3 200 I 1.70 141.58 0.00"),
        new(1, 530, [new(28, "Paint row 4"), new(240, "200"), new(310, "I"), new(360, "0.30"),
            new(430, "24.98"), new(520, "0.00")], "Paint row 4 200 I 0.30 24.98 0.00"),
        new(1, 520, [new(28, "Paint row 5"), new(240, "200"), new(310, "I"), new(360, "0.30"),
            new(430, "24.98"), new(520, "0.00")], "Paint row 5 200 I 0.30 24.98 0.00"),
        new(1, 510, [new(28, "Labour costs"), new(520, "391.42")], "Labour costs 391.42"),
        new(1, 500, [new(28, "Material costs"), new(520, "0.00")], "Material costs 0.00"),
        new(1, 490, [new(28, "Total"), new(520, "391.42")], "Total 391.42"),

        new(1, 470, [new(28, "Summary"), new(100, "totals")], "Summary totals"),
        new(1, 460, [new(28, "Body"), new(240, "83.28"), new(350, "1.00"), new(430, "83.28"),
            new(500, "0.00")], "Body 83.28 1.00 83.28 0.00"),
        new(1, 450, [new(28, "Auxiliary work"), new(240, "83.28"), new(350, "1.00"), new(430, "83.28"),
            new(500, "0.00")], "Auxiliary work 83.28 1.00 83.28 0.00"),
        new(1, 440, [new(28, "Paint"), new(240, "83.28"), new(350, "4.70"), new(430, "391.42"),
            new(500, "0.00")], "Paint 83.28 4.70 391.42 0.00"),
        new(1, 430, [new(28, "Total Labour"), new(350, "6.70"), new(520, "557.98")], "Total Labour 6.70 557.98"),
        new(1, 420, [new(28, "Total Material"), new(520, "0.00")], "Total Material 0.00"),
        new(1, 410, [new(28, "Repair costs excl. VAT"), new(520, "557.98")], "Repair costs excl. VAT 557.98"),
        new(1, 400, [new(28, "VAT (20.00%)"), new(520, "111.60")], "VAT (20.00%) 111.60"),
        new(1, 390, [new(28, "Repair costs incl. VAT"), new(520, "669.58")], "Repair costs incl. VAT 669.58"),

        new(1, 370, [new(28, "Part name"), new(200, "Previous part no."), new(340, "Part no.")],
            "Part name Previous part no. Part no."),
        new(1, 360, [new(28, "Total Parts"), new(520, "0.00")], "Total Parts 0.00"),
        new(1, 340, [new(28, "Part name"), new(220, "Guide positions")], "Part name Guide positions"),
        new(1, 330, [new(28, "Body panel"), new(220, "1001")], "Body panel 1001"),
        new(1, 320, [new(28, "Auxiliary operation"), new(220, "2001")], "Auxiliary operation 2001"),
        new(1, 300, [new(28, "Abbreviations"), new(120, "Codes")], "Abbreviations Codes")
    ];
}
