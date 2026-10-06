using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Assessment;
using Pegasus.Infrastructure.Glass;
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

    /// <summary>
    /// A sheet at <c>10 WU</c> (EVA's Glass's profile, 6 October 2026) prints
    /// every time as a count of work units, ten to the hour, and a blank VIN.
    /// Row, section and total identities hold in printed units, the rate
    /// checks convert to hours, and every time the reader returns is hours.
    /// </summary>
    [Fact]
    public void ASheetAtWorkUnitsToTheHourIsReadInHoursWithABlankVin()
    {
        var parsed = GlassEstimatePdfParser.Parse(WorkUnitDocument("10 WU"));

        Assert.Equal(["repair", "paint_blend", "specialist_wu"], parsed.Lines.Select(line => line.Type));
        Assert.Equal(2.00m, parsed.Lines[0].WorkUnits);
        Assert.Equal(0.30m, parsed.Lines[1].PaintWorkUnits);
        Assert.Equal(0.20m, parsed.Lines[2].WorkUnits);
        Assert.Contains("Printed labour: 166.56 GBP.", parsed.Lines[0].Justification, StringComparison.Ordinal);
        Assert.Equal(2.00m, parsed.SourceTotals!.PanelWorkUnits);
        Assert.Equal(0.30m, parsed.SourceTotals.PaintWorkUnits);
        Assert.Equal(215.90m, parsed.SourceTotals.Net);
        Assert.Equal(43.18m, parsed.SourceTotals.Vat);
        Assert.Equal(259.08m, parsed.SourceTotals.Gross);
        Assert.Equal("AB12 CDE 5.10.2026 TEST", parsed.SourceVersion);
    }

    [Theory]
    [InlineData("1 Day")]
    [InlineData("0 WU")]
    [InlineData("10 Hours")]
    [InlineData("WU")]
    public void ALabourTimeUnitThatIsNeitherOneHourNorWorkUnitsRefusesTheWholeTable(string unit)
    {
        var refusal = Assert.Throws<EstimateParseRejectedException>(
            () => GlassEstimatePdfParser.Parse(WorkUnitDocument(unit)));

        Assert.Contains("units or complete tables are missing", refusal.Message, StringComparison.Ordinal);
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
    [InlineData("LO72XPW", "EVARE35552__CalculationPDF.pdf", "c35d9b4a7404e13bd8d6000c4895f0fb4f4c694ce04d5aa200dc3640f0e71180", 10847,
        "CBD605964C20F5EA200A2765C205C061992B4412E44B16C6BF6DAD4CA52FEAD1", 30, 5, "3346.33", "669.27", "4015.60")]
    [InlineData("KV20VEH", "EVARE35571__CalculationPDF.pdf", "9207cd876f9eb5194915e6d6f53d8f154d5a7f2aaf378cacff9e6d3611bd9674", 19480,
        "C9A33D1FE27DDBDE4456117E04CD2E7E7140C0A16A9BD951201AF9F39ED30E07", 137, 35, "14275.69", "2855.14", "17130.83")]
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
            && "BAPD".Contains(cells[0][0]) && char.IsAsciiDigit(cells[0][1])).ToArray();

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
            var section = source[0][0] switch { 'B' => "body", 'A' => "auxiliary", 'D' => "additional", _ => "paint" };
            var ordinal = int.Parse(source[0].AsSpan(1), CultureInfo.InvariantCulture);
            Assert.Equal($"{section}:p{source[1]}:r{ordinal}:{Blank(source[3])}", actual.SourceRowIdentity);
            Assert.Equal(source[5], actual.Description);
            Assert.Equal(Blank(source[3]), actual.GuideCode);
            Assert.Equal(Number(source[6]), section == "paint" ? actual.PaintWorkUnits : actual.WorkUnits);
            // A part and an additional operation print a unit amount; every
            // other row prints row materials.
            Assert.Equal(Number(source[9]), source[4] is "RP" or "EC" ? actual.Price : actual.Materials);
            if (source[4] == "EC")
                Assert.Equal(Number(source[6]) > 0m ? "specialist_wu" : "specialist_fixed", actual.Type);
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
            // A user-defined body row with hours and an amount: Specialist by
            // work units, its amount kept in Specialist treatment.
            Assert.Equal("specialist_wu", bySource["B59"].Type);
            Assert.Equal(52.14m, bySource["B59"].Price);
            Assert.Equal(2, parsed.Lines.Count(line => string.Equals(line.Description, "Rear camera reset", StringComparison.OrdinalIgnoreCase)));
        }
        if (identity == "LG73ZCJ")
            Assert.Equal("Panel repair sundries (including body fi", bySource["A40"].Description);
        if (identity == "LO72XPW")
        {
            // 10 work units to the hour: the set-up time row prints 2.00 and is 0.20 h,
            // and every time that leaves the reader is in hours.
            Assert.Equal(2.00m, bySource["B01"].WorkUnits);
            Assert.Equal("specialist_wu", bySource["D01"].Type);
            Assert.Equal(0.20m, bySource["D01"].WorkUnits);
            Assert.Equal(0.70m, bySource["P01"].PaintWorkUnits);
            Assert.Equal("paint_blend", bySource["P01"].Type);
            Assert.Equal(15.00m, parsed.SourceTotals.PanelWorkUnits);
            Assert.Equal(5.50m, parsed.SourceTotals.PaintWorkUnits);
        }
        if (identity == "KV20VEH")
        {
            // 12 work units to the hour; the plastic parts' level 5 prints as B (blend).
            Assert.Equal(1.20m, bySource["B01"].WorkUnits);
            Assert.Equal("paint_blend", bySource["P01"].Type);
            Assert.Equal(55.50m, parsed.SourceTotals.PanelWorkUnits);
            Assert.Equal(13.10m, parsed.SourceTotals.PaintWorkUnits);
        }
    }

    /// <summary>
    /// A sheet with Body and Paint only (KY12CAB, 4 September 2026): no
    /// Auxiliary section prints, and the blend level B is a blend.
    /// </summary>
    [Fact]
    public void ABodyAndPaintSheetWithNoAuxiliarySectionIsRead()
    {
        var parsed = GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body", new SheetRow("672201", "Rear Door Outer Handle (L)", "RP", 0.40m, 32.00m, 49.83m))],
            [new PaintRow("Rear Door, Complete K (L)", "200", "B", 0.70m, 137.10m),
             new PaintRow("Prep. metal (on vehicle without pre-painting)", "200", null, 0.70m, 120.16m)],
            [],
            [new PartRow("Rear Door Outer Handle", "6921102934", 49.83m)],
            [new PositionRow("Rear Door Outer Handle", "672201")]));

        Assert.Equal(["new_part", "paint_blend", "paint_prep"], parsed.Lines.Select(line => line.Type));
        Assert.Equal("6921102934", parsed.Lines[0].PartNumber);
        Assert.Equal(0.40m, parsed.SourceTotals!.PanelWorkUnits);
        Assert.Equal(1.40m, parsed.SourceTotals.PaintWorkUnits);
        Assert.Equal(451.09m, parsed.SourceTotals.Net);
        Assert.Equal(90.22m, parsed.SourceTotals.Vat);
        Assert.Equal(541.31m, parsed.SourceTotals.Gross);
    }

    /// <summary>
    /// Glass's own set-up time prints as an Additional costs section
    /// (a.QDOS26066, 5 October 2026): no guide, operation EC, hours 0.20,
    /// labour 16.00. Total Labour hours leave it out and its cost includes it.
    /// </summary>
    [Fact]
    public void AnAdditionalCostsSectionIsASpecialistLineAndItsLabourIsInTheTotal()
    {
        var parsed = GlassEstimatePdfParser.Parse(SetUpTimeSheet("16.00"));

        Assert.Equal(["new_part", "new_part", "paint_new", "specialist_wu"], parsed.Lines.Select(line => line.Type));
        var setUp = parsed.Lines[3];
        Assert.Equal("Set-up time", setUp.Description);
        Assert.Null(setUp.GuideCode);
        Assert.Equal(0.20m, setUp.WorkUnits);
        Assert.Equal(0.00m, setUp.Price);
        Assert.Null(setUp.Materials);
        Assert.Contains("Printed operation: EC.", setUp.Justification, StringComparison.Ordinal);
        Assert.Contains("Printed labour: 16.00 GBP.", setUp.Justification, StringComparison.Ordinal);
        // Total Labour is 22.80 h and 1,840.00: 12.90 + 9.90 hours, and
        // 1,032.00 + 792.00 + 16.00 of labour.
        Assert.Equal(12.90m, parsed.SourceTotals!.PanelWorkUnits);
        Assert.Equal(9.90m, parsed.SourceTotals.PaintWorkUnits);
        Assert.Equal(4235.09m, parsed.SourceTotals.Net);
        Assert.Equal(847.02m, parsed.SourceTotals.Vat);
    }

    [Fact]
    public void AnAdditionalRowWhoseLabourIsNotItsHoursAtTheBodyRateRefusesTheWholeTable()
    {
        var refusal = Assert.Throws<EstimateParseRejectedException>(
            () => GlassEstimatePdfParser.Parse(SetUpTimeSheet("17.00")));

        Assert.Contains("main rows disagree", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// MP23KTV (5 September 2026) prints Mechanical and Auxiliary work, and
    /// operation codes the sheet's own Abbreviations name: S (Sealing) and CA
    /// (Control and adjustment) land as check labour, as the XML's kinds do.
    /// </summary>
    [Fact]
    public void MechanicalWorkAndTheSealingAndControlCodesAreRead()
    {
        var parsed = GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body", new SheetRow("1001", "Wing (L)", "RP", 1.00m, 80.00m, 100.00m)),
             new SectionRows("Mechanical",
                 new SheetRow("2001", "Strut (L)", "R", 1.00m, 80.00m, 0.00m),
                 new SheetRow("NN", "Wiper Arm Cap (R)", "CA", 0.00m, 0m, 0.00m)),
             new SectionRows("Auxiliary work", new SheetRow("NN", "Tailgate", "S", 0.00m, 0m, 0.00m))],
            [new PaintRow("Roof Panel, Centre", "200", "I", 1.00m, 50.00m)],
            [],
            [new PartRow("Wing", "PN100", 100.00m)],
            [new PositionRow("Wing", "1001"), new PositionRow("Strut", "2001"),
             new PositionRow("Wiper Arm Cap", null), new PositionRow("Tailgate", null)]));

        Assert.Equal(
            ["new_part", "repair", "check_labour", "check_labour", "paint_new"],
            parsed.Lines.Select(line => line.Type));
        Assert.Equal("mechanical:p1:r1:2001", parsed.Lines[1].SourceRowIdentity);
        Assert.Equal("auxiliary:p1:r1:NN", parsed.Lines[3].SourceRowIdentity);
        Assert.Contains("Printed operation: CA.", parsed.Lines[2].Justification, StringComparison.Ordinal);
        Assert.Contains("Printed operation: S.", parsed.Lines[3].Justification, StringComparison.Ordinal);
        // Panel work units stay Body and Auxiliary; Mechanical rows are labour lines.
        Assert.Equal(1.00m, parsed.SourceTotals!.PanelWorkUnits);
        Assert.Equal(390.00m, parsed.SourceTotals.Net);
    }

    /// <summary>
    /// "Heated)" ends at x 205.25 and the number starts at 205.0, so the
    /// extractor yields one word, name and number together: it splits at the
    /// main row's own name, in the parts appendix and in the positions one.
    /// </summary>
    [Fact]
    public void ANamePrintedWithNoGapBeforeItsNumberIsSplitAtItsMainRowsName()
    {
        var parsed = GlassEstimatePdfParser.Parse(GluedSheet("Windscreen (Heat Reflecting & Heated)"));

        var windscreen = parsed.Lines[0];
        Assert.Equal("Windscreen (Heat Reflecting & Heated)", windscreen.Description);
        Assert.Equal("LR114261", windscreen.PartNumber);
        Assert.Contains("Position appendix: 768101", windscreen.Justification, StringComparison.Ordinal);
        Assert.Equal(1058.77m, parsed.SourceTotals!.Net);
    }

    [Fact]
    public void AGluedWordThatSplitsAtNoMainRowNameStillRefuses()
    {
        var refusal = Assert.Throws<EstimateParseRejectedException>(
            () => GlassEstimatePdfParser.Parse(GluedSheet("Windscreen Washer Jet")));

        Assert.Contains("no identity", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The paint block of the 5 October 2026 export of forty positions: every
    /// printed level and paint type, the Z flag, the Annotation (price) line,
    /// and the rows with no type and no level.
    /// </summary>
    [Fact]
    public void EveryPrintedPaintLevelTypeAndFlagLandsAsItsLine()
    {
        var parsed = GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body", new SheetRow("672201", "Rear Door Outer Handle (L)", "RP", 0.40m, 32.00m, 49.83m))],
            [new PaintRow("Front Wing, Complete (L)", "200", "III", 1.00m, 80.80m),
             new PaintRow("Bonnet", "310", "I", 2.80m, 739.85m),
             new PaintRow("Front Door, Upper B-C (L)", "200", "SP", 0.90m, 14.32m),
             new PaintRow("Front Door Outer Handle (L)", "200", "K2", 0.30m, 4.10m),
             new PaintRow("Rear Door, Complete K (L)", "200", "IV", 2.10m, 194.70m),
             new PaintRow("Rear Door, Upper B-C (L)", "201", "II", 0.60m, 77.20m, Flag: "Z"),
             new PaintRow("Roof Panel, Centre", "200", "B", 1.30m, 365.00m),
             new PaintRow("Rear Side Panel, Complete K (L)", "340", "I", 3.20m, 397.70m),
             new PaintRow("Tailgate Rear Spoiler", "410", "K1G", 1.30m, 189.10m,
                 Annotation: "Annotation (price) Version Sport"),
             new PaintRow("Rear Bumper k/e", "310", "K1N", 1.80m, 525.55m),
             new PaintRow("Front Door Exterior Mirror (L)", "200", "K1R", 0.40m, 34.40m),
             new PaintRow("Surcharge scratch-resistant clear coat", null, null, 0.10m, 0.00m),
             new PaintRow("Prep. metal (on vehicle without pre-painting)", "200", null, 1.70m, 120.16m),
             new PaintRow("First Colour/Clear tinted coat", null, null, 0.20m, 169.86m),
             new PaintRow("Colour mixing (1)", null, null, 0.30m, 0.00m),
             new PaintRow("Sample colour creation (1)", null, null, 0.30m, 11.62m)],
            [],
            [new PartRow("Rear Door Outer Handle", "6921102934", 49.83m)],
            [new PositionRow("Rear Door Outer Handle", "672201")]));

        Assert.Equal(
            ["new_part",
             "paint_repair", "paint_new", "paint_repair", "paint_repair", "paint_repair", "paint_repair",
             "paint_blend", "paint_new", "paint_new", "paint_new", "paint_new",
             "paint_prep", "paint_prep", "paint_prep", "paint_prep", "paint_prep"],
            parsed.Lines.Select(line => line.Type));
        Assert.Equal("310", parsed.Lines[2].GuideCode);
        Assert.Equal("201", parsed.Lines[6].GuideCode);
        Assert.Contains("Markup material", parsed.Lines[6].Justification, StringComparison.Ordinal);
        Assert.Contains("Printed paint level: II.", parsed.Lines[6].Justification, StringComparison.Ordinal);
        Assert.Contains("Annotation (price) Version Sport", parsed.Lines[9].Justification, StringComparison.Ordinal);
        Assert.Contains("Printed labour: 8.00 GBP.", parsed.Lines[12].Justification, StringComparison.Ordinal);
        Assert.Null(parsed.Lines[12].GuideCode);
        Assert.Equal(0.10m, parsed.Lines[12].PaintWorkUnits);
    }

    [Theory]
    [InlineData("200", "K3")]
    [InlineData("200", "K1")]
    [InlineData("200", "SP2")]
    [InlineData("20", "I")]
    [InlineData("2000", "I")]
    [InlineData("2OO", "I")]
    public void APaintTypeOrLevelOutsideTheTableRefusesTheWholeTable(string type, string level)
    {
        var refusal = Assert.Throws<EstimateParseRejectedException>(() => GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body", new SheetRow("672201", "Rear Door Outer Handle (L)", "RP", 0.40m, 32.00m, 49.83m))],
            [new PaintRow("Panel", type, level, 1.00m, 10.00m)],
            [],
            [new PartRow("Rear Door Outer Handle", "6921102934", 49.83m)],
            [new PositionRow("Rear Door Outer Handle", "672201")])));

        Assert.Contains("paint type or level is unsupported", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APaintRowWithNoLevelAndNoPreparationDescriptionRefuses()
    {
        var refusal = Assert.Throws<EstimateParseRejectedException>(() => GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body", new SheetRow("672201", "Rear Door Outer Handle (L)", "RP", 0.40m, 32.00m, 49.83m))],
            [new PaintRow("Mystery coat", null, null, 1.00m, 10.00m)],
            [],
            [new PartRow("Rear Door Outer Handle", "6921102934", 49.83m)],
            [new PositionRow("Rear Door Outer Handle", "672201")])));

        Assert.Contains("has no level", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A work description Glass's prints under a row ("Adjust headlamps", at
    /// the description's x) is not part of the row's name: the appendix names
    /// the row, and what it does not name under it is the row's note.
    /// </summary>
    [Fact]
    public void ATextLineUnderARowThatTheAppendixDoesNotNameIsItsNoteAndNotItsName()
    {
        var parsed = GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body",
                new SheetRow("T0301ARX", "Xenon Headlamp (with", "RP", 0.00m, 0m, 450.07m,
                    Wrapped: ["Indicator) (L)", "Adjust headlamps", "Annotation (time): Time includes adjustment"],
                    Overlap: 0.60m),
                new SheetRow("S0503XRX", "Bonnet", "RP", 0.60m, 48.00m, 361.74m, Wrapped: ["Adjust bonnet catch"]))],
            [new PaintRow("Prep. metal (on vehicle without pre-painting)", "200", null, 1.00m, 0.00m)],
            [],
            [new PartRow("Xenon Headlamp (with Indicator)", "BR0C51041B", 450.07m),
             new PartRow("Bonnet", "BPYK5231XB", 361.74m)],
            [new PositionRow("Xenon Headlamp (with Indicator)", "T0301ARX"), new PositionRow("Bonnet", "S0503XRX")]));

        var headlamp = parsed.Lines[0];
        Assert.Equal("Xenon Headlamp (with Indicator) (L)", headlamp.Description);
        Assert.Equal("BR0C51041B", headlamp.PartNumber);
        Assert.Contains("Adjust headlamps", headlamp.Justification, StringComparison.Ordinal);
        Assert.Contains("Annotation (time): Time includes adjustment", headlamp.Justification, StringComparison.Ordinal);
        var bonnet = parsed.Lines[1];
        Assert.Equal("Bonnet", bonnet.Description);
        Assert.Equal("BPYK5231XB", bonnet.PartNumber);
        Assert.Contains("Adjust bonnet catch", bonnet.Justification, StringComparison.Ordinal);
    }

    /// <summary>
    /// FRD-25's same-spec rule, run: the XML export and the calculation sheet
    /// for the same rows give the same line types and the same printed net,
    /// whichever paint level the row carries. (The export's embedded sheet is
    /// the document the PDF reader is given; Pegasus does not read the XML's
    /// attachment, so the rows are built once for each reader.)
    /// </summary>
    [Theory]
    [InlineData("B", "3", "I", "paint_new")]
    [InlineData("B", "4", "B", "paint_blend")]
    [InlineData("B", "1", "III", "paint_repair")]
    [InlineData("B", "2", "IV", "paint_repair")]
    [InlineData("B", "6", "SP", "paint_repair")]
    [InlineData("B", "0", "II", "paint_repair")]
    [InlineData("K", "2", "K1R", "paint_new")]
    [InlineData("K", "3", "K1N", "paint_new")]
    [InlineData("K", "4", "K1G", "paint_new")]
    [InlineData("K", "0", "K2", "paint_repair")]
    public void BothGlassesReadersLandTheSameRowsAsTheSameLines(
        string materialKind, string level, string printedLevel, string expectedPaintType) =>
        LandTheSameRowsOnBothRoutes(materialKind, level, printedLevel, expectedPaintType, unitsPerHour: 1);

    /// <summary>
    /// The same rows at 10 work units to the hour: the export's <c>TimeUnit</c>
    /// and the sheet's <c>Labour time unit</c> both say so, every time is
    /// printed in work units, and both routes land the same hours.
    /// </summary>
    [Theory]
    [InlineData("B", "4", "B", "paint_blend")]
    [InlineData("K", "5", "B", "paint_blend")]
    public void BothGlassesReadersLandTheSameWorkUnitRowsAsTheSameLines(
        string materialKind, string level, string printedLevel, string expectedPaintType) =>
        LandTheSameRowsOnBothRoutes(materialKind, level, printedLevel, expectedPaintType, unitsPerHour: 10);

    private static void LandTheSameRowsOnBothRoutes(
        string materialKind, string level, string printedLevel, string expectedPaintType, int unitsPerHour)
    {
        string Printed(decimal hours) =>
            (hours * unitsPerHour).ToString("0.00", CultureInfo.InvariantCulture);
        var sheet = GlassEstimatePdfParser.Parse(Sheet(
            [new SectionRows("Body", new SheetRow("1001", "Body panel", "RP", 1.00m * unitsPerHour, 80.00m, 100.00m))],
            [new PaintRow("Paint row", "200", printedLevel, 1.00m * unitsPerHour, 50.00m, PrintedLabour: 80.00m),
             new PaintRow("Surcharge scratch-resistant clear coat", null, null, 0.10m * unitsPerHour, 0.00m,
                 PrintedLabour: 8.00m),
             new PaintRow("First Colour/Clear tinted coat", null, null, 0.20m * unitsPerHour, 10.00m,
                 PrintedLabour: 16.00m)],
            [new SheetRow("", "Set-up time", "EC", 0.20m * unitsPerHour, 16.00m, 0.00m)],
            [new PartRow("Body panel", "PN1234", 100.00m)],
            [new PositionRow("Body panel", "1001")],
            unit: unitsPerHour == 1 ? "1 Hour" : unitsPerHour.ToString(CultureInfo.InvariantCulture) + " WU"));
        var export = GlassEstimateXmlParser.Read(Encoding.UTF8.GetBytes(GlassEstimateXmlParserTests.GlassExport.BuildXml(
            timeUnit: unitsPerHour == 1 ? "60" : unitsPerHour.ToString(CultureInfo.InvariantCulture),
            positions: string.Concat(
                GlassEstimateXmlParserTests.GlassExport.Position(
                    "Part_SparePart", "Replace", "Body panel", price: "100.00", time: Printed(1.00m), materialCode: "1001"),
                GlassEstimateXmlParserTests.GlassExport.Position(
                    "Paint_Part", "Replace", "Paint row", price: "50.00", time: Printed(1.00m),
                    paintMatKind: materialKind, paintLevel: level),
                GlassEstimateXmlParserTests.GlassExport.Position(
                    "Paint_PreparationScratchResistantClearCoatWork", "Replace",
                    "Surcharge scratch-resistant clear coat", time: Printed(0.10m)),
                GlassEstimateXmlParserTests.GlassExport.Position(
                    "Paint_ClearVarnish", "Replace", "First Colour/Clear tinted coat", price: "10.00", time: Printed(0.20m)),
                GlassEstimateXmlParserTests.GlassExport.Position(
                    "Part_SetUpTime", "Extra costs", "Set-up time", time: Printed(0.20m), materialCode: "20")),
            netTotal: "360.00", vatMaterial: "72.00", grossTotal: "432.00"))).Estimate;

        string[] expected = ["new_part", expectedPaintType, "paint_prep", "paint_prep", "specialist_wu"];
        Assert.Equal(expected, sheet.Lines.Select(line => line.Type));
        Assert.Equal(expected, export.Lines.Select(line => line.Type));
        Assert.Equal(360.00m, sheet.SourceTotals!.Net);
        Assert.Equal(sheet.SourceTotals.Net, export.SourceTotals!.Net);
        // The same hours on both routes, whatever the unit the document counts in.
        Assert.Equal(export.Lines.Select(line => line.WorkUnits), sheet.Lines.Select(line => line.WorkUnits));
        Assert.Equal(export.Lines.Select(line => line.PaintWorkUnits), sheet.Lines.Select(line => line.PaintWorkUnits));
    }

    private static string? Blank(string value) => value == "-" ? null : value;
    private static decimal? Number(string value) => value == "-" ? null
        : decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>One printed operation row; no operation prints an included row, which has no amounts.</summary>
    private sealed record SheetRow(
        string Guide, string Description, string? Operation, decimal Hours, decimal Labour, decimal Material,
        string[]? Wrapped = null, decimal? Overlap = null);

    private sealed record SectionRows(string Label, params SheetRow[] Rows);

    /// <summary>One paint row at the Glass's rate of 80.00 an hour.</summary>
    private sealed record PaintRow(
        string Description, string? Type, string? Level, decimal Hours, decimal Material,
        string? Flag = null, string? Annotation = null, decimal? PrintedLabour = null)
    {
        public decimal Labour => PrintedLabour ?? Hours * 80m;
    }

    private sealed record PartRow(string Name, string Number, decimal Amount, bool Glued = false);

    private sealed record PositionRow(string Name, string? Codes, bool Glued = false);

    private static string Money(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>A name's words at the sheet's pitch; the last carries the number it touches when glued.</summary>
    private static (double X, string Text)[] NameWords(string name, string? glued)
    {
        var words = name.Split(' ');
        var placed = new (double X, string Text)[words.Length];
        var x = 28d;
        for (var index = 0; index < words.Length; index++)
        {
            placed[index] = (x, index == words.Length - 1 && glued is not null ? words[index] + glued : words[index]);
            x += (words[index].Length + 1) * 4.5;
        }
        return placed;
    }

    /// <summary>
    /// A Glass's calculation sheet at the real x positions of the sheets CE's
    /// own account printed (guide 28, description 92, operation 247, hours 318,
    /// overlap 382, labour 467, material 526; paint type 253, level 312, hours
    /// 383, labour 457, material 527, flag 557; appendix number 205). Section
    /// totals, the summary and the document totals are computed from the rows
    /// at the rate of 80.00, with Total Labour hours leaving Additional costs
    /// out and its cost including it, so a test varies one thing at a time.
    /// </summary>
    private static PdfEstimateDocumentParser.VisualRow[] Sheet(
        SectionRows[] sections, PaintRow[] paint, SheetRow[] additional, PartRow[] parts, PositionRow[] positions,
        string unit = "1 Hour")
    {
        List<PdfEstimateDocumentParser.VisualRow> rows = [];
        var y = 800d;
        void Put(params (double X, string Text)[] words)
        {
            y -= 11;
            rows.Add(new(1, y, [.. words.Select(word => new PdfEstimateDocumentParser.PlacedWord(word.X, word.Text))],
                string.Join(' ', words.Select(word => word.Text))));
        }

        Put((28, "Vehicle Registration Number:"), (200, "AB12 CDE"));
        Put((28, "VIN:"), (200, "TESTVIN1234567890"));
        Put((28, "Date:"), (200, "5.10.2026"));
        Put((28, "Database version:"), (200, "TEST"));
        Put((28, "Labour time unit:"), (200, unit));
        Put((28, "Currency:"), (200, "GBP"));

        decimal totalHours = 0, totalLabour = 0, totalMaterial = 0;
        List<(string Label, decimal Hours, decimal Labour, decimal Material)> summary = [];
        foreach (var section in sections)
        {
            Put((28, section.Label), (237, "Repair"), (359, "Overlap-time"));
            Put((239, "type"), (311, "time"), (462, "costs"), (521, "costs"));
            decimal hours = 0, labour = 0, material = 0;
            foreach (var row in section.Rows)
            {
                if (row.Operation is null)
                {
                    Put((33.6, row.Guide), (97.6, row.Description));
                }
                else
                {
                    List<(double X, string Text)> words =
                        [(28, row.Guide), (92, row.Description), (247, row.Operation), (318, Money(row.Hours))];
                    if (row.Overlap is { } overlap) words.Add((382, Money(overlap)));
                    if (row.Labour != 0m) words.Add((467, Money(row.Labour)));
                    words.Add((526, Money(row.Material)));
                    Put([.. words]);
                    hours += row.Hours; labour += row.Labour; material += row.Material;
                }
                foreach (var line in row.Wrapped ?? Array.Empty<string>()) Put((92, line));
            }
            Put((28, "Labour costs"), (465, Money(labour)));
            Put((28, "Material costs"), (530, Money(material)));
            Put((28, "Total " + section.Label), (510, Money(labour + material)));
            summary.Add((section.Label, hours, labour, material));
            totalHours += hours; totalLabour += labour; totalMaterial += material;
        }

        Put((28, "Paint"), (233, "Paint type"), (304, "Paint level"), (512, "Material"));
        Put((302, "level"), (375, "time"), (521, "costs"));
        decimal paintHours = 0, paintLabour = 0, paintMaterial = 0;
        foreach (var row in paint)
        {
            List<(double X, string Text)> words = [(28, row.Description)];
            if (row.Type is not null) words.Add((253, row.Type));
            if (row.Level is not null) words.Add((312, row.Level));
            words.Add((383, Money(row.Hours)));
            words.Add((457, Money(row.Labour)));
            words.Add((527, Money(row.Material)));
            if (row.Flag is not null) words.Add((557, row.Flag));
            Put([.. words]);
            if (row.Annotation is not null) Put((33.6, row.Annotation));
            paintHours += row.Hours; paintLabour += row.Labour; paintMaterial += row.Material;
        }
        Put((28, "Labour costs"), (448, Money(paintLabour)));
        Put((28, "Material costs"), (518, Money(paintMaterial)));
        Put((28, "Total Paint"), (510, Money(paintLabour + paintMaterial)));
        totalHours += paintHours; totalLabour += paintLabour; totalMaterial += paintMaterial;

        decimal additionalLabour = 0, additionalMaterial = 0;
        if (additional.Length > 0)
        {
            Put((28, "Additional costs"), (237, "Repair"), (359, "Overlap-time"));
            Put((239, "type"), (311, "time"), (462, "costs"), (521, "costs"));
            foreach (var row in additional)
            {
                Put((28, row.Description), (247, "EC"), (318, Money(row.Hours)), (467, Money(row.Labour)),
                    (532, Money(row.Material)));
                additionalLabour += row.Labour; additionalMaterial += row.Material;
            }
            Put((28, "Labour costs"), (465, Money(additionalLabour)));
            Put((28, "Material costs"), (530, Money(additionalMaterial)));
            Put((28, "Total Additional costs"), (526, Money(additionalLabour + additionalMaterial)));
            totalLabour += additionalLabour; totalMaterial += additionalMaterial;
        }

        Put((28, "Summary"), (230, "Labour rate"));
        foreach (var (label, hours, labour, material) in summary)
            Put((28, label), (249, "80.00"), (395, Money(hours)), (454, Money(labour)), (515, Money(material)));
        Put((28, "Paint"), (249, "80.00"), (395, Money(paintHours)), (454, Money(paintLabour)), (515, Money(paintMaterial)));
        if (additional.Length > 0)
            Put((28, "Additional costs"), (459, Money(additionalLabour)), (535, Money(additionalMaterial)));
        var net = totalLabour + totalMaterial;
        var vat = decimal.Round(net * 20m / 100m, 2, MidpointRounding.AwayFromZero);
        Put((28, "Total Labour"), (387, Money(totalHours)), (441, Money(totalLabour)));
        Put((28, "Total Material"), (511, Money(totalMaterial)));
        Put((28, "Repair costs excl. VAT"), (511, Money(net)));
        Put((28, "VAT"), (49, "(20.00"), (82, "%)"), (515, Money(vat)));
        Put((28, "Repair costs incl. VAT"), (505.5, Money(net + vat)));

        Put((28, "Part name"), (205, "Part no."), (334, "Previous part no."), (478, "Material costs"));
        foreach (var part in parts)
        {
            List<(double X, string Text)> words = [.. NameWords(part.Name, part.Glued ? part.Number : null)];
            if (!part.Glued) words.Add((205, part.Number));
            words.Add((528, Money(part.Amount)));
            Put([.. words]);
        }
        Put((28, "Total Parts"), (509.5, Money(parts.Sum(part => part.Amount))));

        Put((28, "Part name"), (205, "Position no."));
        foreach (var position in positions)
        {
            List<(double X, string Text)> words = [.. NameWords(position.Name, position.Glued ? position.Codes : null)];
            if (!position.Glued && position.Codes is not null) words.Add((205, position.Codes));
            Put([.. words]);
        }
        Put((28, "Abbreviations"), (205, "Description"));
        return [.. rows];
    }

    /// <summary>Body, Paint and Additional costs with the set-up time row's labour as printed (a.QDOS26066).</summary>
    private static PdfEstimateDocumentParser.VisualRow[] SetUpTimeSheet(string setUpLabour) => Sheet(
        [new SectionRows("Body",
            new SheetRow("671000", "Roof Panel", "RP", 12.90m, 1032.00m, 1354.76m),
            new SheetRow("672201", "Rear Door Outer Handle (L)", "RP", 0.00m, 0m, 49.83m))],
        [new PaintRow("Roof Panel, Centre", "200", "I", 9.90m, 990.50m)],
        [new SheetRow("", "Set-up time", "EC", 0.20m, decimal.Parse(setUpLabour, CultureInfo.InvariantCulture), 0.00m)],
        [new PartRow("Roof Panel", "PN-ROOF", 1354.76m), new PartRow("Rear Door Outer Handle", "6921102934", 49.83m)],
        [new PositionRow("Roof Panel", "671000"), new PositionRow("Rear Door Outer Handle", "672201")]);

    /// <summary>A windscreen whose name ends where the number column starts, in both appendices.</summary>
    private static PdfEstimateDocumentParser.VisualRow[] GluedSheet(string partName) => Sheet(
        [new SectionRows("Body",
            new SheetRow("768101", "Windscreen (Heat Reflecting", "RP", 0.00m, 0m, 978.77m,
                Wrapped: ["& Heated)"], Overlap: 1.90m))],
        [new PaintRow("Prep. metal (on vehicle without pre-painting)", "200", null, 1.00m, 0.00m)],
        [],
        [new PartRow(partName, "LR114261", 978.77m, Glued: true)],
        [new PositionRow("Windscreen (Heat Reflecting & Heated)", "768101", Glued: true)]);

    /// <summary>
    /// Body, Paint and Additional costs at the Glass's rate of 83.28 an hour,
    /// every printed time a count of work units, and a VIN line printed with
    /// nothing after it (the EVA sheets). At ten units to the hour the body
    /// row is 2.00 h, the paint row 0.30 h and the set-up time 0.20 h.
    /// </summary>
    private static PdfEstimateDocumentParser.VisualRow[] WorkUnitDocument(string unit) =>
    [
        new(1, 760, [new(28, "Vehicle Registration Number:"), new(200, "AB12 CDE")], "Vehicle Registration Number: AB12 CDE"),
        new(1, 750, [new(28, "VIN:")], "VIN:"),
        new(1, 740, [new(28, "Date:"), new(200, "5.10.2026")], "Date: 5.10.2026"),
        new(1, 730, [new(28, "Database version:"), new(200, "TEST")], "Database version: TEST"),
        new(1, 720, [new(28, "Labour time unit:"), new(200, unit)], $"Labour time unit: {unit}"),
        new(1, 710, [new(28, "Currency:"), new(200, "GBP")], "Currency: GBP"),

        new(1, 690, [new(28, "Body"), new(359, "Overlap-time")], "Body Overlap-time"),
        new(1, 680, [new(28, "NN"), new(92, "Rear Bumper Lining"), new(247, "R"), new(318, "20.00"),
            new(467, "166.56"), new(526, "0.00")], "NN Rear Bumper Lining R 20.00 166.56 0.00"),
        new(1, 670, [new(28, "Labour costs"), new(465, "166.56")], "Labour costs 166.56"),
        new(1, 660, [new(28, "Material costs"), new(530, "0.00")], "Material costs 0.00"),
        new(1, 650, [new(28, "Total Body"), new(510, "166.56")], "Total Body 166.56"),

        new(1, 630, [new(28, "Paint"), new(233, "Paint type"), new(304, "Paint level"), new(512, "Material")],
            "Paint Paint type Paint level Material"),
        new(1, 620, [new(302, "level"), new(375, "time"), new(521, "costs")], "level time costs"),
        new(1, 610, [new(28, "Fuel Filler Lid (L)"), new(253, "200"), new(312, "B"), new(383, "3.00"),
            new(457, "24.98"), new(527, "7.70")], "Fuel Filler Lid (L) 200 B 3.00 24.98 7.70"),
        new(1, 600, [new(28, "Labour costs"), new(448, "24.98")], "Labour costs 24.98"),
        new(1, 590, [new(28, "Material costs"), new(518, "7.70")], "Material costs 7.70"),
        new(1, 580, [new(28, "Total Paint"), new(510, "32.68")], "Total Paint 32.68"),

        new(1, 560, [new(28, "Additional costs"), new(359, "Overlap-time")], "Additional costs Overlap-time"),
        new(1, 550, [new(28, "Set-up time"), new(247, "EC"), new(318, "2.00"), new(467, "16.66"), new(532, "0.00")],
            "Set-up time EC 2.00 16.66 0.00"),
        new(1, 540, [new(28, "Labour costs"), new(465, "16.66")], "Labour costs 16.66"),
        new(1, 530, [new(28, "Material costs"), new(530, "0.00")], "Material costs 0.00"),
        new(1, 520, [new(28, "Total Additional costs"), new(526, "16.66")], "Total Additional costs 16.66"),

        new(1, 500, [new(28, "Summary"), new(230, "Labour rate")], "Summary Labour rate"),
        new(1, 490, [new(28, "Body"), new(249, "83.28"), new(395, "20.00"), new(454, "166.56"), new(515, "0.00")],
            "Body 83.28 20.00 166.56 0.00"),
        new(1, 480, [new(28, "Paint"), new(249, "83.28"), new(395, "3.00"), new(454, "24.98"), new(515, "7.70")],
            "Paint 83.28 3.00 24.98 7.70"),
        new(1, 470, [new(28, "Additional costs"), new(459, "16.66"), new(535, "0.00")], "Additional costs 16.66 0.00"),
        new(1, 460, [new(28, "Total Labour"), new(387, "23.00"), new(441, "208.20")], "Total Labour 23.00 208.20"),
        new(1, 450, [new(28, "Total Material"), new(511, "7.70")], "Total Material 7.70"),
        new(1, 440, [new(28, "Repair costs excl. VAT"), new(511, "215.90")], "Repair costs excl. VAT 215.90"),
        new(1, 430, [new(28, "VAT"), new(49, "(20.00"), new(82, "%)"), new(515, "43.18")], "VAT (20.00 %) 43.18"),
        new(1, 420, [new(28, "Repair costs incl. VAT"), new(505.5, "259.08")], "Repair costs incl. VAT 259.08"),

        new(1, 400, [new(28, "Part name"), new(205, "Part no."), new(334, "Previous part no."), new(478, "Material costs")],
            "Part name Part no. Previous part no. Material costs"),
        new(1, 390, [new(28, "Total Parts"), new(509.5, "0.00")], "Total Parts 0.00"),
        new(1, 370, [new(28, "Part name"), new(205, "Position no.")], "Part name Position no."),
        new(1, 360, [new(28, "Rear Bumper Lining")], "Rear Bumper Lining"),
        new(1, 340, [new(28, "Abbreviations"), new(205, "Description")], "Abbreviations Description"),
    ];

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
