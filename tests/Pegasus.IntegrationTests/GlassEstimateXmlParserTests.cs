using System.Globalization;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047 B04: the Glass's ERE export parser against synthetic
/// <c>&lt;Estimation&gt;</c> documents shaped exactly like the reference
/// exports — the same elements, the same position types and repair kinds, the
/// same <c>TimeUnit</c> 60 and the same printed statistics block — but with
/// this repository's own figures and the documented estate registration. No
/// real export, registration, VIN or customer detail is committed.
///
/// <para>
/// Money and time are asserted exactly. The fixture is internally consistent
/// the way a real export is — its positions' chargeable hours times its own
/// labour rate equal the labour cost it prints — so a parser that read
/// <c>Time</c> in the wrong unit, or that costed a row's overlap or inclusive
/// time twice, fails here rather than in an estimate.
/// </para>
/// </summary>
public sealed class GlassEstimateXmlParserTests
{
    /// <summary>Every default fixture line as "{type}|{description}", in order.</summary>
    private static readonly string[] ExpectedLines =
    [
        "new_part|Front Suspension Strut",
        "repair|Front Wing",
        "rnr|Front Bumper Cover",
        "check_labour|Headlamp Alignment",
        "check_labour|Front Screen Sealing",
        "check_labour|Front Wheel Alignment",
        "check_labour|Bleed Brake System",
        "new_part|Radiator Air Guide",
        "paint_new|Bonnet",
        "paint_repair|Front Wing",
        "paint_prep|Preparation, metal",
        "paint_prep|Preparation, synthetics",
        "paint_prep|Colour mixing",
        "paint_prep|Sample colour creation",
    ];

    private readonly GlassEstimateXmlParser parser = new();

    [Theory]
    [InlineData("estimation.xml", "application/octet-stream", true)]
    [InlineData("ESTIMATION.XML", "", true)]
    [InlineData("export.bin", "application/xml", true)]
    [InlineData("export.bin", "text/xml", true)]
    [InlineData("estimate.pdf", "application/pdf", false)]
    [InlineData("estimate.json", "application/json", false)]
    public void CanParseRecognizesXmlByNameOrMediaType(string fileName, string mediaType, bool expected) =>
        Assert.Equal(expected, parser.CanParse(fileName, mediaType));

    [Fact]
    public void TheRouteIsGlasses() =>
        Assert.Equal(RepairSpecificationSourceRoute.Glasses, parser.Route);

    [Fact]
    public void OnlyAnEstimationRootIsRecognized()
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(
            () => Parse("<Valuation><Calculation /></Valuation>"));

        Assert.Contains("'Valuation'", rejected.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was imported", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryObservedPositionTypeAndRepairKindMapsToItsOwnLine()
    {
        var result = Parse(GlassExport.BuildXml());

        Assert.Equal(GlassEstimateXmlParser.ProviderName, result.ProviderName);
        Assert.Equal("2.2.1 2026-09-04T17:34:13Z", result.SourceVersion);
        Assert.Equal(
            ExpectedLines,
            result.Lines.Select(line => $"{line.Type}|{line.Description}").ToArray());

        // The row's own identity in the document is its ordinal and the
        // Glass's material code it carries.
        Assert.Equal("1:4016001", result.Lines[0].SourceRowIdentity);
        Assert.Equal("4016001", result.Lines[0].GuideCode);
        Assert.Equal("54650CJ010", result.Lines[0].PartNumber);
    }

    /// <summary>
    /// The unit test in the literal sense: at <c>TimeUnit</c> 60 a position's
    /// <c>Time</c> is decimal hours, and the document's own labour cost proves
    /// it. Reading the same figures as sixtieths would produce 0.135 hours and
    /// £10.80 of labour against the £648.00 this document prints.
    /// </summary>
    [Fact]
    public void ChargeableHoursAtTheDocumentsOwnRateReproduceItsPrintedLabourCost()
    {
        var result = Parse(GlassExport.BuildXml());

        var hours = result.Lines.Sum(line => (line.WorkUnits ?? 0m) + (line.PaintWorkUnits ?? 0m));

        Assert.Equal(8.10m, hours);
        Assert.Equal(GlassExport.PrintedLabourCost, hours * GlassExport.LabourRate);
    }

    [Fact]
    public void OverlapTimeIsDeductedAndAnInclusivePartAddsNoTime()
    {
        var result = Parse(GlassExport.BuildXml());

        // Stated 1.00 hours sharing 0.20 with another row.
        Assert.Equal(0.80m, result.Lines[0].WorkUnits);
        // An inclusive part states 0.50 hours its parent row already carries.
        Assert.Equal("Radiator Air Guide", result.Lines[7].Description);
        Assert.Equal(0m, result.Lines[7].WorkUnits);
    }

    [Fact]
    public void TimeIsKeptAtTheDocumentsPrecisionAndNeverRoundedToTheEditorsStep()
    {
        var result = Parse(GlassExport.BuildXml(
            positions: GlassExport.Position(
                "Part_SparePart", "Replace", "Wiper Arm Cap", price: "1.53", time: "0.016667")));

        Assert.Equal(0.016667m, Assert.Single(result.Lines).WorkUnits);
    }

    [Fact]
    public void TimeBeyondTheEstimatesOwnPrecisionOrBoundIsRefused()
    {
        Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            positions: GlassExport.Position("Part_SparePart", "Replace", "Wing", time: "0.0166675"))));
        Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            positions: GlassExport.Position("Part_SparePart", "Replace", "Wing", time: "1000.1"))));
    }

    [Fact]
    public void APartRowPricesThePartAndAPaintRowPricesItsMaterial()
    {
        var result = Parse(GlassExport.BuildXml());

        var part = result.Lines[0];
        Assert.Equal(100.00m, part.Price);
        Assert.Null(part.Materials);
        Assert.Null(part.PaintWorkUnits);
        Assert.False(part.Unpriced);

        var paint = result.Lines[8];
        Assert.Equal("paint_new", paint.Type);
        Assert.Equal(200.00m, paint.Materials);
        Assert.Null(paint.Price);
        Assert.Equal(2.00m, paint.PaintWorkUnits);
        Assert.Null(paint.WorkUnits);

        // Pegasus costs the estimate from these rows: parts from the part
        // rows' prices, the paint bucket from the paint rows' materials.
        Assert.Equal(
            GlassExport.PrintedPartsTotal,
            result.Lines.Sum(line => line.Price ?? 0m));
        Assert.Equal(
            GlassExport.PrintedPaintTotal,
            result.Lines.Sum(line => line.Materials ?? 0m));
    }

    [Fact]
    public void ThePrintedTotalsAreReturnedAsEvidence()
    {
        var totals = Parse(GlassExport.BuildXml()).SourceTotals;

        Assert.NotNull(totals);
        Assert.Equal(GlassExport.PrintedPartsTotal, totals.Parts);
        Assert.Equal(GlassExport.PrintedPaintTotal, totals.Materials);
        Assert.Equal(0.00m, totals.Specialist);
        Assert.Equal(1058.00m, totals.Net);
        Assert.Equal(211.60m, totals.Vat);
        Assert.Equal(1269.60m, totals.Gross);
        // Glass's prints a labour cost, never a work-unit count, and no count
        // is back-derived from its rate.
        Assert.Null(totals.PanelWorkUnits);
        Assert.Null(totals.PaintWorkUnits);
    }

    /// <summary>
    /// A printed figure that disagrees with the rows is recorded exactly as
    /// printed. Nothing is dropped, nothing is reconciled and the import is
    /// not refused: Pegasus costs the estimate from its own rows and keeps the
    /// disagreement beside that calculation.
    /// </summary>
    [Fact]
    public void APrintedTotalThatDisagreesWithTheRowsIsRecordedAndNotResolved()
    {
        var result = Parse(GlassExport.BuildXml(partsTotal: "999.99"));

        Assert.Equal(999.99m, result.SourceTotals?.Parts);
        Assert.Equal(GlassExport.PrintedPartsTotal, result.Lines.Sum(line => line.Price ?? 0m));
    }

    /// <summary>
    /// An ERE calculation saved before any damage was costed exports no
    /// position, no attachment and statistics printed to six places.
    /// </summary>
    [Fact]
    public void AnExportWithNoPositionIsAValidEmptyEstimate()
    {
        var export = Read(GlassExport.BuildXml(
            positions: string.Empty,
            partsTotal: "0.000000",
            labourTotal: "0.000000",
            paintTotal: "0.000000",
            additionalTotal: "0.000000",
            netTotal: "0.000000",
            vatMaterial: "0.000000",
            vatLabour: "0.000000",
            grossTotal: "0.000000",
            attachment: string.Empty));

        Assert.Empty(export.Estimate.Lines);
        Assert.Equal(0m, export.Estimate.SourceTotals?.Parts);
        Assert.Null(export.CalculationSheet);
    }

    [Fact]
    public void MoreThanTheEstimatesOwnLineBoundIsRefused()
    {
        var positions = string.Concat(Enumerable.Repeat(
            GlassExport.Position("Part_SparePart", "Replace", "Wing", price: "1.00", time: "0.10"),
            AssessmentPolicy.MaximumEstimateLines + 1));

        var rejected = Assert.Throws<EstimateParseRejectedException>(
            () => Parse(GlassExport.BuildXml(positions: positions)));

        Assert.Contains("more than", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIdentityTheGatewayReconcilesIsSurfacedAndNotEnforced()
    {
        var identity = Read(GlassExport.BuildXml()).Identity;

        Assert.Equal("AB12CDE", identity.RegistrationPlate);
        Assert.Equal(33000, identity.Mileage);
        Assert.Equal("1", identity.MileageUnitCode);
        Assert.Equal("123456789", identity.TypeNumber);
        Assert.Null(identity.Vin);
    }

    [Fact]
    public void AnUnreadableMileageIsRefused() =>
        Assert.Throws<EstimateParseRejectedException>(
            () => Parse(GlassExport.BuildXml(mileage: "thirty three thousand")));

    [Fact]
    public void TheEmbeddedCalculationSheetIsDecoded()
    {
        var sheet = Read(GlassExport.BuildXml()).CalculationSheet;

        Assert.NotNull(sheet);
        Assert.Equal("CalculationPDF.pdf", sheet.FileName);
        Assert.Equal(GlassExport.CalculationSheetBytes, sheet.Content.ToArray());
    }

    [Theory]
    // An attachment of another type is refused rather than retained as a PDF.
    [InlineData("XML", "JVBERi0xLjQKJSVFT0YK")]
    // Base64 that decodes to something that never began as a PDF.
    [InlineData("PDF", "bm90IGEgcGRmIGF0IGFsbCwgbm90IGV2ZW4gY2xvc2U=")]
    // A PDF header with no end marker: a truncated download, not a document.
    [InlineData("PDF", "JVBERi0xLjQKdHJ1bmNhdGVk")]
    // Not base64 at all.
    [InlineData("PDF", "!!!! not base64 !!!!")]
    public void AnAttachmentThatIsNotAWholePdfIsRefused(string type, string document)
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            attachment: $"<Attachment Type=\"{type}\"><Name>CalculationPDF.pdf</Name>"
                + $"<Document>{document}</Document></Attachment>")));

        Assert.Contains("attachment", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAttachmentNameThatIsNotAFileNameIsRefused() =>
        Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            attachment: "<Attachment Type=\"PDF\"><Name>../../etc/passwd</Name>"
                + $"<Document>{GlassExport.CalculationSheetBase64}</Document></Attachment>")));

    /// <summary>
    /// The body below is a whole, well-formed export; only its document type
    /// definition is added. The reader refuses it for that alone.
    /// </summary>
    [Fact]
    public void ADocumentTypeDefinitionIsRefused()
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(
            () => Parse(GlassExport.WithDoctype("<!DOCTYPE Estimation [<!ELEMENT Estimation ANY>]>")));

        Assert.Contains("could not be read", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExternalEntityIsNeverResolved() =>
        Assert.Throws<EstimateParseRejectedException>(
            () => Parse(GlassExport.WithDoctype(
                "<!DOCTYPE Estimation [<!ENTITY external SYSTEM \"file:///etc/passwd\">]>")));

    [Fact]
    public void AnEntityExpansionIsNeverPerformed() =>
        Assert.Throws<EstimateParseRejectedException>(
            () => Parse(GlassExport.WithDoctype(
                "<!DOCTYPE Estimation [ "
                + "<!ENTITY a \"aaaaaaaaaa\"> "
                + "<!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\"> "
                + "<!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\"> ]>")));

    [Fact]
    public void AnOversizeDocumentIsRefusedUnread()
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(
            () => parser.Parse(new byte[GlassEstimateXmlParser.MaximumDocumentBytes + 1]));

        Assert.Contains("larger than", rejected.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AByteOrderMarkAtEitherEndIsTolerated(bool leading, bool trailing)
    {
        byte[] mark = [0xEF, 0xBB, 0xBF];
        var body = Encoding.UTF8.GetBytes(GlassExport.BuildXml());
        var content = new List<byte>();
        if (leading)
        {
            content.AddRange(mark);
        }
        content.AddRange(body);
        if (trailing)
        {
            content.AddRange(mark);
        }

        Assert.Equal(14, parser.Parse(content.ToArray()).Lines.Count);
    }

    [Theory]
    [InlineData("Part_Wheel")]
    [InlineData("Paint_Blend")]
    [InlineData("")]
    public void AnUnknownPositionTypeIsRefusedAndNeverGuessed(string posType)
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            positions: GlassExport.Position(posType, "Replace", "Wing", time: "1.00"))));

        Assert.Contains("Position 1", rejected.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Overhaul")]
    [InlineData("")]
    public void AnUnknownRepairKindIsRefusedAndNeverGuessed(string repairKind)
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            positions: GlassExport.Position("Part_SparePart", repairKind, "Wing", time: "1.00"))));

        Assert.Contains("Position 1", rejected.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1O0.00", "1.00")]
    [InlineData("-1.00", "1.00")]
    [InlineData("1.005", "1.00")]
    [InlineData("100.00", "one hour")]
    [InlineData("100.00", "-1.00")]
    public void AnUnreadableAmountRefusesTheWholeImport(string price, string time)
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            positions: GlassExport.Position("Part_SparePart", "Replace", "Wing", price: price, time: time))));

        Assert.Contains("nothing was imported", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MoreOverlapTimeThanTimeIsRefused() =>
        Assert.Throws<EstimateParseRejectedException>(() => Parse(GlassExport.BuildXml(
            positions: GlassExport.Position(
                "Part_SparePart", "Replace", "Wing", time: "0.50", overlapTime: "0.60"))));

    [Theory]
    [InlineData("100")]
    [InlineData("1")]
    [InlineData("")]
    public void AnyTimeUnitButSixtyIsRefusedRatherThanGuessedAt(string timeUnit)
    {
        var rejected = Assert.Throws<EstimateParseRejectedException>(
            () => Parse(GlassExport.BuildXml(timeUnit: timeUnit)));

        Assert.Contains("time unit", rejected.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnExportWithNoCalculationIsRefused() =>
        Assert.Throws<EstimateParseRejectedException>(
            () => Parse("<Estimation><GlobalSetting><XMLDocVers>2.2.1</XMLDocVers></GlobalSetting></Estimation>"));

    private ParsedEstimate Parse(string xml) => parser.Parse(Encoding.UTF8.GetBytes(xml));

    private static GlassEstimateExport Read(string xml) =>
        GlassEstimateXmlParser.Read(Encoding.UTF8.GetBytes(xml));

    /// <summary>
    /// The synthetic <c>&lt;Estimation&gt;</c> fixture: the reference exports'
    /// element shape and vocabulary with this repository's own figures. The
    /// default document is internally consistent — 8.10 chargeable hours at
    /// £80.00 is the £648.00 of labour it prints, its part prices are the
    /// £100.00 of parts and its paint prices the £310.00 of paint.
    /// </summary>
    private static class GlassExport
    {
        /// <summary>The export's own XML declaration, exactly as written below.</summary>
        internal const string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";

        internal const decimal LabourRate = 80.00m;
        internal const decimal PrintedLabourCost = 648.00m;
        internal const decimal PrintedPartsTotal = 100.00m;
        internal const decimal PrintedPaintTotal = 310.00m;

        /// <summary>The smallest thing that is a whole PDF: a header and an end marker.</summary>
        internal static byte[] CalculationSheetBytes { get; } =
            Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< >>\nendobj\ntrailer\n<< >>\n%%EOF\n");

        internal static string CalculationSheetBase64 { get; } = Convert.ToBase64String(CalculationSheetBytes);

        internal static string BuildXml(
            string? positions = null,
            string timeUnit = "60",
            string? attachment = null,
            string mileage = "33000",
            string partsTotal = "100.00",
            string labourTotal = "648.00",
            string paintTotal = "310.00",
            string additionalTotal = "0.00",
            string netTotal = "1058.00",
            string vatMaterial = "211.60",
            string vatLabour = "0.00",
            string grossTotal = "1269.60") =>
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Estimation>
              <GlobalSetting>
                <EtgCountryCd>EN</EtgCountryCd>
                <XMLDocVers>2.2.1</XMLDocVers>
              </GlobalSetting>
              <FileDamage>
                <Setting><Created>2026-09-05T16:41:41Z</Created></Setting>
              </FileDamage>
              <Vehicle Origin="CalcVehicle">
                <Identification>
                  <MakeText>Test Make</MakeText>
                  <ModelText>Test Model</ModelText>
                  <TypeNo>123456789</TypeNo>
                  <MilUnit>1</MilUnit>
                  <Mileage>{mileage}</Mileage>
                  <RegPlt>AB12CDE</RegPlt>
                </Identification>
              </Vehicle>
              <Valuation Function="Calculation load and recalculate, user can modify">
                <Date>2026-09-04</Date>
              </Valuation>
              <Calculation Function="Calculation load and recalculate, user can modify">
                <Setting>
                  <Version>2.2.1</Version>
                  <Created>2026-09-04T18:32:25</Created>
                  <Modified>2026-09-04T17:34:13Z</Modified>
                </Setting>
                <CalcSetting><Currency>GBP</Currency></CalcSetting>
                <Rate>
                  <LabourRate>
                    <PanelBeater>{LabourRate.ToString("0.00", CultureInfo.InvariantCulture)}</PanelBeater>
                    <Painter>{LabourRate.ToString("0.00", CultureInfo.InvariantCulture)}</Painter>
                  </LabourRate>
                  <Vat><VATMaterial>20.00</VATMaterial><VATLabour>20.00</VATLabour></Vat>
                  <Other><PartIndex>1.00</PartIndex><TimeUnit>{timeUnit}</TimeUnit></Other>
                </Rate>
                <Criteria />
                {positions ?? DefaultPositions}
                <Result>
                  <ExclVatResults>
                    <Total1>{netTotal}</Total1>
                    <TotRepCostExclVat>{netTotal}</TotRepCostExclVat>
                    <DiscOverall>0.00</DiscOverall>
                    <PrevDamDeduct>0.00</PrevDamDeduct>
                  </ExclVatResults>
                  <InclVatResults>
                    <VatMat>{vatMaterial}</VatMat>
                    <VatWork>{vatLabour}</VatWork>
                    <TotRepCostInclVat>{grossTotal}</TotRepCostInclVat>
                    <SelfKeptDeduct>0.00</SelfKeptDeduct>
                    <GrandTotal>{grossTotal}</GrandTotal>
                  </InclVatResults>
                  <ExclVatStatisticResults>
                    <TotalAmountParts>{partsTotal}</TotalAmountParts>
                    <TotalAmountLabourCosts>{labourTotal}</TotalAmountLabourCosts>
                    <TotalAmountPaint>{paintTotal}</TotalAmountPaint>
                    <TotalAmountAdditionalCosts>{additionalTotal}</TotalAmountAdditionalCosts>
                  </ExclVatStatisticResults>
                </Result>
                <Internal />
              </Calculation>
              {attachment ?? DefaultAttachment}
            </Estimation>
            """;

        /// <summary>
        /// The default export with a document type definition after its XML
        /// declaration — the one thing that changes.
        /// </summary>
        internal static string WithDoctype(string doctype) =>
            BuildXml().Replace(Declaration, Declaration + doctype, StringComparison.Ordinal);

        /// <summary>One Position with the elements this parser reads.</summary>
        internal static string Position(
            string posType,
            string repairKind,
            string text,
            string price = "0.00",
            string time = "0.00",
            string overlapTime = "0.00",
            string materialCode = "4016001",
            string oemPartNo = "") =>
            $"""
            <Position>
              <PosType>{posType}</PosType>
              <GroupId>1</GroupId>
              <MCode>{materialCode}</MCode>
              <Text>{text}</Text>
              <MatKind>K</MatKind>
              <OEMPartNo>{oemPartNo}</OEMPartNo>
              <ManPartNo />
              <Price>{price}</Price>
              <EtgPrice>{price}</EtgPrice>
              <Time>{time}</Time>
              <EtgTime>{time}</EtgTime>
              <OverlapTime>{overlapTime}</OverlapTime>
              <RepairKind>{repairKind}</RepairKind>
              <PriceMarker>false</PriceMarker>
              <TimeMarker>false</TimeMarker>
              <AlterMarker>false</AlterMarker>
              <UserPosMarker />
              <PaintKind>-1</PaintKind>
              <PaintLevel>-1</PaintLevel>
              <PaintTreatment>0</PaintTreatment>
              <PaintMethod>0</PaintMethod>
              <Place>L</Place>
              <OperationNr />
              <OldForNewValue>0.00</OldForNewValue>
            </Position>
            """;

        private static string DefaultAttachment { get; } =
            $"""
            <Attachment Type="PDF">
              <Name>CalculationPDF.pdf</Name>
              <Comment>2026-09-04;  EN;  Test Model</Comment>
              <Document>{CalculationSheetBase64}</Document>
            </Attachment>
            """;

        /// <summary>
        /// Every position type and repair kind the reference exports carry:
        /// a priced part sharing overlap time, a repair, a remove-and-refit,
        /// the four labour-only kinds, an inclusive part whose time its parent
        /// already carries, two painted panels and the four paint preparation
        /// rows.
        /// </summary>
        private static string DefaultPositions { get; } = string.Concat(
            Position("Part_SparePart", "Replace", "Front Suspension Strut",
                price: "100.00", time: "1.00", overlapTime: "0.20", oemPartNo: "54650CJ010"),
            Position("Part_SparePart", "Repair", "Front Wing", time: "2.00", materialCode: "1063200"),
            Position("Part_SparePart", "Uninstall and install", "Front Bumper Cover",
                time: "0.50", materialCode: "1010810"),
            Position("Part_SparePart", "Control", "Headlamp Alignment", time: "0.30", materialCode: "1023850"),
            Position("Part_SparePart", "Sealing", "Front Screen Sealing", time: "0.10", materialCode: "1017001"),
            Position("Part_SparePart", "Adjust", "Front Wheel Alignment", time: "0.10", materialCode: "4020001"),
            Position("Part_SparePart", "Air out", "Bleed Brake System", time: "0.20", materialCode: "7028820"),
            Position("Part_InclusiveSparePart", "Replace", "Radiator Air Guide",
                time: "0.50", materialCode: "6093340"),
            Position("Paint_Part", "Replace", "Bonnet", price: "200.00", time: "2.00", materialCode: "1640"),
            Position("Paint_Part", "Repair", "Front Wing", price: "50.00", time: "1.00", materialCode: "1240"),
            Position("Paint_PreparationMetal", "Replace", "Preparation, metal",
                price: "30.00", time: "0.50", materialCode: "2"),
            Position("Paint_PreparationPlastic", "Replace", "Preparation, synthetics",
                price: "20.00", time: "0.30", materialCode: "3"),
            Position("Paint_ColourMixing", "Replace", "Colour mixing", time: "0.20", materialCode: "6"),
            Position("Paint_ColourSample", "Replace", "Sample colour creation",
                price: "10.00", time: "0.10", materialCode: "7"));
    }
}
