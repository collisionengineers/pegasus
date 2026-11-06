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

/// <summary>
/// The Repair Spec printout is the EVA breakdown (operator, 6 November 2026;
/// R750 to R754). Its measurements are the EVA example's, in millimetres from
/// the page's top left.
/// </summary>
public sealed partial class EstimateDocumentRendererTests
{
    private const string ColumnHeadings = "Qty Description Type Labour Paint Litres Mat Price Anti-C A/C Price Price";

    private static readonly string[] IdentityLines =
        ["Date:16/09/2026", "OurRef:QDOS26001", "Claimant:AlexExample", "Vehicle:FordFocus", "Registration:AB12CDE"];

    private static readonly double[] IdentityBaselines = [46.23, 53.25, 60.20, 67.22, 73.24];

    private static readonly string[] SummaryLabels =
    [
        "New", "Repair", "Paint", "R & R", "Blend", "Specialist", "Check", "Total Hours",
        "Labour Rate", "Labour Disc", "Material Dis", "Parts Dis", "Labour", "Materials", "Parts", "Other",
        "Labour Discount", "Materials Discount", "Parts Discount", "Other Discounts",
        "Total Disc", "Nett", "Vat", "Gross",
    ];

    private static readonly string[] SnapshotFigures = ["Door skin", "Paint door", "1.00", "£160.63", "£4.00"];

    /// <summary>The logo band and the EVA band: left, top, width and height.</summary>
    private static readonly (double, double, double, double)[] Bands =
        [(5.1, 0.0, 195.0, 44.0), (5.1, 264.1, 195.0, 26.0)];

    [Fact]
    public async Task TheRendererPublishesItsEngineVersionWithoutRendering()
    {
        await using var provider = Provider();

        var version = provider.GetRequiredService<IEstimateDocumentRenderer>().EngineVersion;

        Assert.StartsWith("QuestPDF/", version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheEstimateDocumentPrintsTheEvaBlocksAndNothingElse()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();

        var artifact = await renderer.RenderAsync(Snapshot());

        Assert.Equal(EstimateDocumentContract.TemplateVersion, artifact.TemplateVersion);
        Assert.Equal(renderer.EngineVersion, artifact.EngineVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(artifact.Pdf)), artifact.Sha256);
        Assert.EndsWith("_estimate.pdf", artifact.SuggestedFileName, StringComparison.Ordinal);
        var text = string.Join(" ", PageTexts(artifact.Pdf));
        Assert.Contains(ColumnHeadings, text, StringComparison.Ordinal);
        foreach (var expected in SummaryLabels.Concat(SnapshotFigures))
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
        // What the EVA breakdown has no place for is not printed (R753).
        foreach (var absent in new[] { "PN-42", "CLAIM-1", "Your Ref", "Estimate 1", "DRAFT", "Manual", "Page 1" })
        {
            Assert.DoesNotContain(absent, text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The example's own estimate prints the example's figures, and the
    /// first page is laid out as the example's is.
    /// </summary>
    [Fact]
    public async Task TheEvaExamplesEstimatePrintsTheExamplesBreakdown()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var estimate = Estimate(
            new EstimateDetails("EVA estimate", 103.06m, null, 20m,
                EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            EvaExampleLines());

        var artifact = await renderer.RenderAsync(Document(estimate));

        Assert.Equal(2, artifact.PageCount);
        var texts = PageTexts(artifact.Pdf);
        Assert.Contains("1 Rear Bumper New £277.36", texts[0], StringComparison.Ordinal);
        Assert.Contains("1 Rear Bumper New Part Paint K1r Paint 1.80 £232.29", texts[0], StringComparison.Ordinal);
        Assert.Contains("1 Pre-Painting Sundry Materials Paint 1 Preparation", texts[0], StringComparison.Ordinal);
        foreach (var expected in new[]
        {
            "11.10", "9.60", "20.70", "£103.06", "0.00%",
            "£2,133.35", "£1,432.44", "£338.90", "£426.07",
            "Total Disc £0.00", "Nett £4,330.76", "Vat £866.15", "Gross £5,196.91",
        })
        {
            Assert.Contains(expected, texts[1], StringComparison.Ordinal);
        }

        var page = Read(artifact.Pdf)[0];
        Assert.Equal(
            Bands,
            page.Images.OrderBy(image => image.Top)
                .Select(image => (Math.Round(image.Left, 1), Math.Round(image.Top, 1),
                    Math.Round(image.Width, 1), Math.Round(image.Height, 1))));
        var identity = Lines(page.Letters.Where(letter => letter.Size == 10.08));
        Assert.Equal(IdentityLines, identity.Select(line => line.Text));
        for (var index = 0; index < identity.Count; index++)
        {
            Assert.InRange(identity[index].Baseline, IdentityBaselines[index] - 0.2, IdentityBaselines[index] + 0.2);
        }
        var rows = Lines(page.Letters.Where(letter => letter.Size == 7.44));
        Assert.All(rows, row => Assert.True(row.Bold));
        var heading = rows[0];
        Assert.Equal(Squeeze(ColumnHeadings), heading.Text);
        Assert.InRange(heading.Baseline, 86.2, 86.6);
        Assert.InRange(rows[1].Baseline, 90.2, 90.6);
        Assert.InRange(rows[^1].Baseline - rows[1].Baseline, 159.8, 160.2);
        Assert.Contains(page.Shapes, shape => shape.Fill == "#dcdcdc" && Math.Abs(shape.Top - 92.05) < 0.1);
    }

    /// <summary>
    /// The logo band, the identity lines and the EVA band print on the first
    /// page only; the line table's heading repeats on every page it runs to,
    /// and the summary prints once, after the table (R754).
    /// </summary>
    [Fact]
    public async Task ALongEstimateContinuesWithTheTableAloneAndOneSummary()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var source = Estimate(Enumerable.Range(1, 120)
            .Select(index => Line(index, $"Repair line {index:000} with a retained description that wraps across the available cell width"))
            .ToArray());

        var artifact = await renderer.RenderAsync(Document(source));

        var texts = PageTexts(artifact.Pdf);
        var pages = Read(artifact.Pdf);
        Assert.True(pages.Count > 1);
        Assert.Equal(pages.Count, artifact.PageCount);
        Assert.Equal(2, pages[0].Images.Count);
        Assert.Contains("Registration:", texts[0], StringComparison.Ordinal);
        for (var index = 0; index < pages.Count; index++)
        {
            // A page the table runs onto repeats its heading; the summary, moved
            // whole, may stand on a page of its own, as the example's page 2 does.
            if (texts[index].Contains("Repair line", StringComparison.Ordinal))
            {
                Assert.Contains(ColumnHeadings, texts[index], StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(pages.Count - 1, index);
                Assert.DoesNotContain(ColumnHeadings, texts[index], StringComparison.Ordinal);
            }
            if (index > 0)
            {
                Assert.Empty(pages[index].Images);
                Assert.DoesNotContain("Registration:", texts[index], StringComparison.Ordinal);
            }
        }
        var all = string.Join(" ", texts);
        for (var index = 1; index <= 120; index++)
        {
            Assert.Equal(1, Regex.Count(all, $"Repair line {index:000}"));
        }
        Assert.Equal(1, Regex.Count(all, "Total Hours"));
        Assert.Equal(1, Regex.Count(all, "Gross"));
        Assert.Contains("Total Hours", texts[^1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The category boxes print before discount, each discount box what its
    /// discounts took off it, and the Nett is the categories less the Total
    /// Disc (R751).
    /// </summary>
    [Fact]
    public async Task DiscountsPrintAsTheCategoriesBeforeDiscountLessWhatTheyTookOff()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var estimate = Estimate(
            DefaultDetails() with
            {
                LabourRate = 100m,
                OtherCosts = null,
                Discounts = new EstimateDiscounts(0.1m, 0.05m, 0m, 0.03m),
            },
            Line(1, "Panel") with { Type = "rnr", WorkUnits = 2m },
            Line(2, "Part") with { Type = "new_part", WorkUnits = null, Price = 100m });

        var artifact = await renderer.RenderAsync(Document(estimate));
        var text = string.Join(" ", PageTexts(artifact.Pdf));

        // Labour £200 less 3 % is £194; parts £100 less 10 % then 3 % is £87.30.
        foreach (var expected in new[]
        {
            "0.00% 5.00% 10.00%", "£200.00 £0.00 £100.00 £0.00",
            "£6.00 £0.00 £12.70 £0.00", "Total Disc £18.70", "Nett £281.30",
        })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
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
            estimate, "QDOS26001", new(2026, 9, 16),
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

    /// <summary>The EVA example's 41 lines, at its £103.06 rate.</summary>
    private static CaseEstimateLineRecord[] EvaExampleLines()
    {
        var position = 0;
        CaseEstimateLineRecord Part(string description, decimal price) =>
            Line(++position, description) with { Type = "new_part", WorkUnits = null, Price = price };
        CaseEstimateLineRecord Refit(string description, decimal hours) =>
            Line(++position, description) with { Type = "rnr", WorkUnits = hours };
        CaseEstimateLineRecord Paint(string type, string description, decimal? hours = null, decimal? materials = null) =>
            Line(++position, description) with { Type = type, WorkUnits = null, PaintWorkUnits = hours, Materials = materials };
        CaseEstimateLineRecord Fixed(string description, decimal price) =>
            Line(++position, description) with { Type = "specialist_fixed", WorkUnits = null, Price = price };
        return
        [
            Part("Rear Bumper", 277.36m),
            Part("L/R S/Panel Protect", 30.77m),
            Part("R/R S/Panel Protect", 30.77m),
            Refit("Renew L/R Side Panel Protector", 0.10m),
            Refit("Renew R/R Side Panel Protector", 0.10m),
            Refit("Renew Rear Bumper", 0.90m),
            Refit("Inner End Panel Trim", 0.30m),
            Refit("L/Load Compartment Trim", 0.30m),
            Refit("L/Outer Rear Lamp", 0.10m),
            Refit("R/Outer Rear Lamp", 0.10m),
            Refit("Ramp Load & Unload", 0.20m),
            Refit("Bumper To Stand", 0.20m),
            Refit("Specialist Qc And Road Test", 1.00m),
            Refit("Specialist Pre Repair Clean", 0.50m),
            Refit("Specialist Post Repair Check", 1.00m),
            Refit("Specialist Pre Repair Check", 1.00m),
            Refit("Specialist Wash And Vacuum", 1.00m),
            Refit("Specialist Standard Shutdown", 1.00m),
            Refit("Specialist Yard Charge", 1.00m),
            Refit("Specialist Ev / Hybrid Risk", 0.50m),
            Refit("Rr Bumper 2Nd Man Lift", 0.30m),
            Paint("paint_new", "Rear Bumper New Part Paint K1r", 1.80m, 232.29m),
            Paint("paint_repair", "L/Upp A-Pillar Surface Paint", 0.60m, 77.43m),
            Paint("paint_repair", "R/Upp A-Pillar Surface Paint", 0.60m, 77.43m),
            Paint("paint_repair", "L/Roof Section Surface Paint", 0.70m, 90.33m),
            Paint("paint_repair", "R/Roof Section Surface Paint", 0.70m, 90.33m),
            Paint("paint_repair", "L/R Side Panel Surface Paint", 1.00m, 129.05m),
            Paint("paint_repair", "R/R Side Panel Surface Paint", 1.00m, 129.05m),
            Paint("paint_repair", "Denib / Polish Paint", 1.50m, 193.57m),
            Paint("paint_repair", "Additional Mask Frt Screen", 0.20m, 25.81m),
            Paint("paint_repair", "Additional Mask Fuel Aperature", 0.20m, 25.81m),
            Paint("paint_repair", "Additional Masking Rr Screen", 0.30m, 38.71m),
            Fixed("Assessment Fee", 176.96m),
            Fixed("Epa Charge", 31.23m),
            Fixed("Vehicle Care Kit", 10.41m),
            Fixed("Paint Consumable", 30.00m),
            Fixed("Collect/Deliver", 150.00m),
            Fixed("Glass Lifting Tape", 15.61m),
            Paint("paint_prep", "Pre-Painting Sundry Materials"),
            Paint("paint_prep", "Preparation for Pre-Painting", 2.50m, 322.63m),
            Fixed("Sundry Part", 11.86m),
        ];
    }

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
