using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Assessment;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-002: the Audatex estimate parser against synthetic PDFs that
/// reproduce the format's real geometry — a numeric column printed on its
/// own baseline one point below the description row, stable per-section
/// column positions, and the document's own printed section totals. The
/// fixtures are built in-test with PdfPig's writer; no real estimate is
/// committed. Money is asserted exactly: a parser that pairs a value with
/// the wrong line fails these tests, and a document whose lines do not add
/// up to its own totals must reject the whole import.
/// </summary>
public sealed class AudatexEstimatePdfParserTests
{
    private readonly AudatexEstimatePdfParser parser = new();

    [Theory]
    [InlineData("estimate.pdf", "application/octet-stream", true)]
    [InlineData("ESTIMATE.PDF", "", true)]
    [InlineData("estimate.bin", "application/pdf", true)]
    [InlineData("estimate.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", false)]
    public void CanParseRecognizesPdfByNameOrMediaType(string fileName, string mediaType, bool expected) =>
        Assert.Equal(expected, parser.CanParse(fileName, mediaType));

    [Fact]
    public void TheRouteIsAudatexPdf() =>
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, parser.Route);

    [Fact]
    public void ParsesEveryLineWithItsOwnMoney()
    {
        var result = parser.Parse(AudatexEstimateFixture.Build());

        Assert.Equal("TEST01 V1/1", result.SourceVersion);
        Assert.Equal(6, result.Lines.Count);

        var labourOne = result.Lines[0];
        Assert.Equal("rnr", labourOne.Type);
        Assert.Equal("12 34 567", labourOne.GuideCode);
        Assert.Equal("R + R FRONT BUMPER", labourOne.Description);
        Assert.Equal(9.0m, labourOne.WorkUnits);
        Assert.Null(labourOne.Price);

        // The continuation row under this line carries text, never money.
        var labourTwo = result.Lines[1];
        Assert.Equal("repair", labourTwo.Type);
        Assert.Equal("REPAIR WING (TRIM REMOVED)", labourTwo.Description);
        Assert.Equal(12.0m, labourTwo.WorkUnits);

        var paint = result.Lines[2];
        Assert.Equal("paint_new", paint.Type);
        Assert.Equal(16.2m, paint.WorkUnits);

        var pricedPart = result.Lines[3];
        Assert.Equal("new_part", pricedPart.Type);
        Assert.Equal("FRONT BUMPER", pricedPart.Description);
        Assert.Equal(620.20m, pricedPart.Price);
        Assert.Equal("51 11 8 067", pricedPart.PartNumber);
        Assert.Equal("0%", pricedPart.Betterment);
        Assert.False(pricedPart.Unpriced);

        var unpricedPart = result.Lines[4];
        Assert.Equal("GRILLE BADGE", unpricedPart.Description);
        Assert.True(unpricedPart.Unpriced);
        Assert.Null(unpricedPart.Price);

        var specialist = result.Lines[5];
        Assert.Equal("specialist_fixed", specialist.Type);
        Assert.Equal("4 WHEEL ALIGNMENT", specialist.Description);
        Assert.Equal(110.00m, specialist.Price);

        // Every imported line is a provisional proposal until acceptance,
        // and the whole set passes the one shared line normalization.
        Assert.All(result.Lines, line => Assert.Equal("provisional", line.Status));
        var normalized = AssessmentPolicy.NormalizeRepairSpecificationLines(result.Lines);
        Assert.Equal(result.Lines.Count, normalized.Count);
    }

    [Fact]
    public void TheDocumentNamesItsProviderItsPrintedTotalsAndEachRowsIdentity()
    {
        var result = parser.Parse(AudatexEstimateFixture.Build());

        Assert.Equal(AudatexEstimatePdfParser.ProviderName, result.ProviderName);

        // The document's own printed section totals are carried as evidence:
        // Pegasus still costs the estimate from the rows at its own rate,
        // discounts and VAT categories, and a figure that disagrees with that
        // calculation is retained beside it rather than adopted.
        var totals = Assert.IsType<EstimateSourceTotals>(result.SourceTotals);
        Assert.Equal(21.0m, totals.PanelWorkUnits);
        Assert.Equal(16.2m, totals.PaintWorkUnits);
        Assert.Equal(620.20m, totals.Parts);
        Assert.Equal(110.00m, totals.Specialist);
        Assert.Null(totals.Net);

        // A row's identity is its section and its ordinal within that section,
        // so it survives the four sections being concatenated into one set.
        Assert.Equal(
            ["labour:1", "labour:2", "paint:1", "parts:1", "parts:2", "extras:1"],
            result.Lines.Select(line => line.SourceRowIdentity));
    }

    [Fact]
    public void RejectsWhenPartsDoNotAddUpToTheDocumentsOwnSubTotal()
    {
        var bytes = AudatexEstimateFixture.Build(partsSubTotal: "£999.99");

        var rejection = Assert.Throws<EstimateParseRejectedException>(() => parser.Parse(bytes));
        Assert.Contains("parts", rejection.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was imported", rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsWhenWorkUnitsDoNotAddUpToTheDocumentsOwnTotal()
    {
        var bytes = AudatexEstimateFixture.Build(labourTotalWorkUnits: "20.0");

        var rejection = Assert.Throws<EstimateParseRejectedException>(() => parser.Parse(bytes));
        Assert.Contains("labour", rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAValueThatCannotBeMatchedToItsLine()
    {
        // A second amount for the same line has no valueless line to pair with.
        var bytes = AudatexEstimateFixture.Build(extraOrphanAmount: true);

        var rejection = Assert.Throws<EstimateParseRejectedException>(() => parser.Parse(bytes));
        Assert.Contains("could not be matched", rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsWhenTheDocumentIdentityIsMissing()
    {
        var bytes = AudatexEstimateFixture.Build(includeIdentity: false);

        var rejection = Assert.Throws<EstimateParseRejectedException>(() => parser.Parse(bytes));
        Assert.Contains("assessment number and version", rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAPdfThatIsNotAnAudatexReport()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("A letter about something else entirely.", 9, new PdfPoint(20, 400), font);

        var rejection = Assert.Throws<EstimateParseRejectedException>(
            () => parser.Parse(builder.Build()));
        Assert.Contains("not recognized as an Audatex estimate", rejection.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsBytesThatAreNotAPdf()
    {
        var rejection = Assert.Throws<EstimateParseRejectedException>(
            () => parser.Parse("not a pdf"u8.ToArray()));
        Assert.Contains("could not be read as a PDF", rejection.Message, StringComparison.Ordinal);
    }
}
