using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

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
        foreach (var expected in new[]
        {
            "ESTIMATE", "QDOS26001", "CLAIM-1", "Alex Example", "Ford Focus",
            "AB12 CDE", "Estimate 1", "DRAFT", "Door skin", "PN-42",
            "Additional materials", "Additional costs", "Hours", "Rate and discounts",
            "Labour", "Materials", "Parts", "Specialist / Other", "Net", "Gross",
        })
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
        Assert.Contains("1.75", text, StringComparison.Ordinal);
        Assert.Contains("2.75", text, StringComparison.Ordinal);
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
    public async Task AMaximumLengthSavedEstimateNameWrapsInsideTheLetterhead()
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
    }

    [Fact]
    public async Task TheGoldenFixedSpecialistFixturePrintsItsOwnedMoneyFigures()
    {
        await using var provider = Provider();
        var renderer = provider.GetRequiredService<IEstimateDocumentRenderer>();
        var estimate = Estimate(
            new EstimateDetails("EVA estimate", 3, 83.28m, 510.58m, 238.60m, 20m, null,
                EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            Line(1, "Panel repairs") with { WorkUnits = 12.3m },
            Line(2, "Paint operations") with
            {
                Type = "paint_repair", WorkUnits = null, PaintWorkUnits = 4.7m,
            },
            Line(3, "Replacement part") with { Type = "new_part", WorkUnits = null, Price = 160.63m },
            Line(4, "Tyre") with { Type = "specialist_fixed", WorkUnits = null, Price = 180m },
            Line(5, "Alignment") with { Type = "specialist_fixed", WorkUnits = null, Price = 112.42m },
            Line(6, "Operation 1") with { Type = "specialist_fixed", WorkUnits = 1m },
            Line(7, "Operation 2") with { Type = "specialist_fixed", WorkUnits = 1m },
            Line(8, "Operation 3") with { Type = "specialist_fixed", WorkUnits = 1m },
            Line(9, "Operation 4") with { Type = "specialist_fixed", WorkUnits = 1m },
            Line(10, "Operation 5") with { Type = "specialist_fixed", WorkUnits = 1m });

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
        lines, null, "engineer", new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero),
        null, null, null, null, details);

    private static EstimateDetails DefaultDetails() => new(
        "Estimate 1", 3, 83.28m, 12m, 8m, 20m, null,
        EstimateDiscounts.None, EstimateVatPolicy.For(RepairerVatStatus.Registered));

    private static CaseEstimateLineRecord Line(int position, string description) => new(
        Guid.NewGuid(), position, "repair", null, description, 0.1m, null, false,
        null, null, null, null, null, ActorKind.Staff, "engineer",
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero), "engineer",
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
