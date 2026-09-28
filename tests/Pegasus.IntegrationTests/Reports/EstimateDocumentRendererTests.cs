using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using static Pegasus.IntegrationTests.Reports.PrintedPages;

namespace Pegasus.IntegrationTests.Reports;

public sealed partial class EstimateDocumentRendererTests
{
    [Fact]
    public async Task TheRendererPublishesItsEngineVersionWithoutRendering()
    {
        await using var provider = Provider();

        var version = provider.GetRequiredService<IEstimateDocumentRenderer>().EngineVersion;

        Assert.StartsWith("QuestPDF/", version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheEstimateDocumentRendersEveryOwnedBlock()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var snapshot = Snapshot();

        var artifact = await renderer.RenderAsync(snapshot);

        Assert.Equal(1, artifact.PageCount);
        Assert.Equal(EstimateDocumentContract.TemplateVersion, artifact.TemplateVersion);
        Assert.Equal(renderer.EngineVersion, artifact.EngineVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf)), artifact.Sha256);
        Assert.EndsWith("_estimate.pdf", artifact.SuggestedFileName, StringComparison.Ordinal);
        var text = string.Join(" ", PageTexts(artifact.Pdf));
        // The title's letters are spaced, so it is looked for with the spaces taken out.
        Assert.Contains("ESTIMATE", Squeeze(text), StringComparison.Ordinal);
        foreach (var expected in new[]
        {
            "QDOS26001", "CLAIM-1", "Alex Example", "Ford Focus",
            "AB12 CDE", "Estimate 1", "DRAFT", "Door skin", "PN-42",
            "Additional costs", "Hours", "Rate and discounts",
            "Labour", "Materials", "Parts", "Specialist / Other", "Net", "Gross",
        })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
        Assert.Contains("1.75", text, StringComparison.Ordinal);
        Assert.Contains("2.75", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Repair Spec printout takes the report's running header, footer,
    /// margins, title and heading style (operator, 27 September 2026), on
    /// every page it runs to.
    /// </summary>
    [Fact]
    public async Task ThePrintoutTakesTheReportsPage()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var snapshot = Document(Estimate(Enumerable.Range(1, 120)
            .Select(index => Line(index, $"Repair line {index:000}"))
            .ToArray()) with
        {
            Details = DefaultDetails() with { OtherCosts = 25m },
        });

        var artifact = await renderer.RenderAsync(snapshot);

        Assert.Equal("estimate-v2", artifact.TemplateVersion);
        var pages = Read(artifact.Pdf);
        Assert.True(pages.Count > 1);
        for (var index = 0; index < pages.Count; index++)
        {
            var page = pages[index];
            // The company block against the right edge, 190 mm, and the logo at the left.
            var block = Lines(page.Letters.Where(letter => letter.Baseline < 36));
            Assert.Equal(
                AssessmentReportWording.CompanyBlock(feeNote: false).Select(Squeeze),
                block.Select(line => line.Text));
            Assert.All(block, line => Assert.InRange(line.Right, 189.5, 190.5));
            var logo = Assert.Single(page.Images);
            Assert.Equal((20.0, 8.2, 44.0), (Math.Round(logo.Left, 1), Math.Round(logo.Top, 1), Math.Round(logo.Width, 1)));

            // The footer beneath its red rule, centred, and the page number at the right.
            var foot = Lines(page.Letters.Where(letter => letter.Baseline > 280));
            Assert.Equal(
                [
                    Squeeze(AssessmentReportWording.Footer("AB12 CDE", "QDOS26001", feeNote: false)),
                    Squeeze(AssessmentReportWording.PageNumber(index + 1, pages.Count)),
                ],
                foot.OrderBy(line => line.Left).Select(line => line.Text));
            Assert.All(foot, line => Assert.Equal("#777777", line.Colour));
            var footer = foot.MinBy(line => line.Left)!;
            Assert.InRange((footer.Left + footer.Right) / 2, 104.5, 105.5);
            Assert.Contains(page.Shapes, shape =>
                shape.Stroke == "#c80a32" && Math.Abs(shape.Left - 39) < 0.5 && Math.Abs(shape.Right - 171) < 0.5
                && Math.Abs(shape.Top - 282.26) < 0.5);

            // The body keeps the report's margins.
            var body = page.Letters.Where(letter => letter.Baseline is > 36 and < 280).ToArray();
            Assert.InRange(body.Min(letter => letter.Left), 22.0, 22.3);
            Assert.InRange(body.Max(letter => letter.Right), 22.0, 188.0);
        }

        // The title and the headings are the report's: 16 point bold italic
        // and 11 point bold, red, with no rule beneath a heading.
        var title = Assert.Single(Lines(pages[0].Letters.Where(letter => letter.Size == 16)));
        Assert.Equal(("ESTIMATE", "#c80a32", true, true), (title.Text, title.Colour, title.Bold, title.Italic));
        Assert.InRange((title.Left + title.Right) / 2, 104.5, 105.5);
        var headings = pages.SelectMany(page => Lines(page.Letters.Where(letter => letter.Size == 11))).ToArray();
        Assert.Equal(["Adjustments", "Hours", "Rateanddiscounts", "Totals"], headings.Select(line => line.Text));
        Assert.All(headings, heading =>
        {
            Assert.Equal(("#c80a32", true, false), (heading.Colour, heading.Bold, heading.Italic));
            Assert.InRange(heading.Left, 22.0, 22.3);
        });
    }

    [Fact]
    public async Task ALongEstimatePaginatesWithRepeatedIdentityAndHeaders()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var source = Estimate(Enumerable.Range(1, 120)
            .Select(index => Line(index, $"Repair line {index:000} with a retained description that wraps across the available cell width"))
            .ToArray());
        var snapshot = Document(source);

        var artifact = await renderer.RenderAsync(snapshot);
        var pages = PageTexts(artifact.Pdf);

        Assert.True(pages.Length > 1);
        Assert.All(pages, page =>
        {
            Assert.Contains("Estimate 1 · v1", page, StringComparison.Ordinal);
            Assert.Contains("DRAFT", page, StringComparison.Ordinal);
            Assert.Contains("Qty Description Type Hours Paint Materials Unit price", page, StringComparison.Ordinal);
        });
        var all = string.Join(" ", pages);
        for (var index = 1; index <= 120; index++)
        {
            Assert.Equal(1, Regex.Count(all, $"Repair line {index:000}"));
        }
        Assert.Equal(1, Regex.Count(all, "Rate and discounts"));
        Assert.Equal(1, Regex.Count(all, "Gross"));
    }

    [Fact]
    public async Task AMaximumLengthSavedEstimateNameWrapsWithinTheBody()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var estimateName = new string('W', EstimatePolicy.MaximumNameLength);
        var estimate = Estimate(Line(1, "Repair line")) with
        {
            Details = DefaultDetails() with { Name = estimateName },
        };

        var artifact = await renderer.RenderAsync(Document(estimate));
        var text = string.Join(" ", PageTexts(artifact.Pdf));

        Assert.Equal(1, artifact.PageCount);
        Assert.Equal(EstimatePolicy.MaximumNameLength * 2, text.Count(character => character == 'W'));
        Assert.Contains("Estimate:", text, StringComparison.Ordinal);
        // Both printings of the name wrap between the body's margins.
        var name = Assert.Single(Read(artifact.Pdf)).Letters.Where(letter => letter.Value == "W").ToArray();
        Assert.InRange(name.Min(letter => letter.Left), 22.0, 188.0);
        Assert.InRange(name.Max(letter => letter.Right), 22.0, 188.0);
    }

    [Fact]
    public async Task TheGoldenFixedSpecialistFixturePrintsItsOwnedMoneyFigures()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var estimate = Estimate(
            new EstimateDetails("EVA estimate", 83.28m, null, 20m,
                EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            EvaEstimateLines("specialist_fixed"));

        var artifact = await renderer.RenderAsync(Document(estimate));
        var text = string.Join(" ", PageTexts(artifact.Pdf));

        foreach (var expected in new[]
        {
            "£1,415.76", "£510.58", "£160.63", "£531.02",
            "£2,617.99", "£523.60", "£3,141.59",
        })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
        Assert.Contains("17.00", text, StringComparison.Ordinal);
        Assert.Contains("5.00", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheGoldenCheckLabourFixturePrintsTheCorrectlyTypedLabourHoursAndNet()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var estimate = Estimate(
            new EstimateDetails("EVA estimate", 83.28m, null, 20m,
                EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            EvaEstimateLines("check_labour"));

        var artifact = await renderer.RenderAsync(Document(estimate));
        var text = string.Join(" ", PageTexts(artifact.Pdf));

        Assert.Contains("£1,832.16", text, StringComparison.Ordinal);
        Assert.Contains("22.00", text, StringComparison.Ordinal);
        Assert.Contains("£3,034.39", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidSnapshotFailsBeforeRendering()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();

        await Assert.ThrowsAsync<ReportRenderRejectedException>(() =>
            renderer.RenderAsync(Snapshot() with { Lines = [] }));
    }

    private static EstimateDocumentSnapshot Snapshot() => Document(Estimate(
        Line(1, "Door skin") with { PartNumber = "PN-42", Price = 160.63m, WorkUnits = 1m },
        Line(2, "Paint door") with
        {
            Type = "paint_repair", WorkUnits = 0.75m, PaintWorkUnits = 1m, Materials = 4m,
        }));

    private static EstimateDocumentSnapshot Document(RepairSpecificationVersion estimate) =>
        EstimateDocumentSnapshot.For(
            estimate, "QDOS26001", "CLAIM-1", new(2026, 9, 16),
            "Alex Example", "Ford Focus", "AB12 CDE");

    private static RepairSpecificationVersion Estimate(params CaseEstimateLineRecord[] lines) =>
        Estimate(DefaultDetails(), lines);

    private static RepairSpecificationVersion Estimate(
        EstimateDetails details,
        params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        lines, "engineer", new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
        details);

    private static CaseEstimateLineRecord[] EvaEstimateLines(string timedOperationType) =>
    [
        Line(1, "Right Front Door Membrane") with { Type = "new_part", WorkUnits = 0.1m, Price = 103.18m },
        Line(2, "Right Front Door Protective Moulding") with { Type = "new_part", WorkUnits = null, Price = 43.19m },
        Line(3, "Rear Bumper Lining") with { Type = "rnr", WorkUnits = 0.8m },
        Line(4, "Right Rear Side Panel") with { WorkUnits = 10m },
        Line(5, "Right Side Panel Protective Moulding") with { Type = "new_part", WorkUnits = 0.1m, Price = 14.26m },
        Line(6, "Right Front Door Strip & Set-Up for Paint") with { Type = "rnr", WorkUnits = 1.3m },
        Line(7, ".Assessment Damage Appraisal Charge") with { Type = "specialist_fixed", WorkUnits = null, Price = 176.96m },
        Line(8, ".Environmental Charge") with { Type = "specialist_fixed", WorkUnits = null, Price = 31.23m },
        Line(9, ".QC & Road Test") with { Type = timedOperationType, WorkUnits = 1m },
        Line(10, ".Standard shutdown") with { Type = timedOperationType, WorkUnits = 1m },
        Line(11, ".Sundries") with { Type = "specialist_fixed", WorkUnits = null, Price = 20m },
        Line(12, ".System Diagnostic Check (Post Repair)") with { Type = timedOperationType, WorkUnits = 1m },
        Line(13, ".System Diagnostic Check (Pre Repair)") with { Type = timedOperationType, WorkUnits = 1m },
        Line(14, ".Vehicle Care Kit") with { Type = "specialist_fixed", WorkUnits = null, Price = 10.41m },
        Line(15, ".Wash/Clean") with { Type = timedOperationType, WorkUnits = 1m },
        Line(16, "OSR tyre") with { Type = "specialist_fixed", WorkUnits = null, Price = 180m },
        Line(17, "Wheel Alignment (check)") with { Type = "specialist_fixed", WorkUnits = null, Price = 112.42m },
        Line(18, "Right Front Door, Complete") with { Type = "paint_repair", WorkUnits = null, PaintWorkUnits = 0.8m, Materials = 191.20m },
        Line(19, "Right Rear Side Panel, Complete") with { Type = "paint_repair", WorkUnits = null, PaintWorkUnits = 1.6m, Materials = 187.60m },
        Line(20, "Prep. metal (on vehicle without pre-painting)") with { Type = "paint_prep", WorkUnits = null, PaintWorkUnits = 1.7m, Materials = 120.16m },
        Line(21, "Colour mixing (1)") with { Type = "paint_repair", WorkUnits = null, PaintWorkUnits = 0.3m },
        Line(22, "Sample colour creation (1)") with { Type = "paint_repair", WorkUnits = null, PaintWorkUnits = 0.3m, Materials = 11.62m },
    ];

    private static EstimateDetails DefaultDetails() => new(
        "Estimate 1", 83.28m, 8m, 20m,
        EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered));

    private static CaseEstimateLineRecord Line(int position, string description) => new(
        Guid.NewGuid(), position, "repair", null, description, 0.1m, null, false,
        null, null, null, null, ActorKind.Staff, "engineer",
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));

    private static string[] PageTexts(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.GetPages()
            .Select(page => Whitespace().Replace(ContentOrderTextExtractor.GetText(page), " ").Trim())
            .ToArray();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();


    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
