using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using UglyToad.PdfPig;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The page count a rendered artifact reports is the count of the pages its PDF
/// holds (efficiency register A1-01, R243), and what the render costs beside
/// the PdfPig open that used to read it.
/// </summary>
public sealed class ReportPageCountTests(ITestOutputHelper output)
{
    private const int Renders = 15;

    [Fact]
    public async Task EveryDocumentKindReportsThePageCountItsPdfHolds()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = AssessmentReportRendererTests.ReadySnapshot();
        var thirteen = Photos(snapshot, 13);
        var seven = thirteen[..7];

        var cases = new (string Name, AssessmentReportSnapshot Snapshot, CaseReportArtifactKind Kind)[]
        {
            ("report", snapshot, CaseReportArtifactKind.AssessmentReport),
            ("report-without-vehicle-images", snapshot with { IncludeVehicleImages = false }, CaseReportArtifactKind.AssessmentReport),
            ("fee-note", snapshot, CaseReportArtifactKind.FeeNote),
            ("pack-of-7", snapshot with { Photos = seven }, CaseReportArtifactKind.ImagePack),
            ("pack-of-13", snapshot with { Photos = thirteen }, CaseReportArtifactKind.ImagePack),
        };
        foreach (var (name, source, kind) in cases)
        {
            var artifact = await renderer.RenderAsync(source, kind);

            using var document = PdfDocument.Open(artifact.Pdf);
            Assert.True(artifact.PageCount > 0, name);
            Assert.Equal(document.NumberOfPages, artifact.PageCount);
        }
    }

    /// <summary>
    /// Times <c>RenderAsync</c> for the seven-page report and the thirteen-photo
    /// pack, and the PdfPig open of the bytes each returned, with the process's
    /// allocation per render. It asserts that each render holds pages and no
    /// speed: the figures go to the test's output, and CI's run shows them.
    /// </summary>
    [Fact]
    public async Task TheRendersAreTimedBesideTheOpenThePageCountUsedToNeed()
    {
        await using var provider = RendererProvider();
        var renderer = provider.GetRequiredService<IAssessmentReportRenderer>();
        var snapshot = AssessmentReportRendererTests.ReadySnapshot();
        var thirteen = Photos(snapshot, 13);

        await Time("report-7-pages", renderer, snapshot, CaseReportArtifactKind.AssessmentReport);
        await Time("image-pack-13-photos", renderer, snapshot with { Photos = thirteen }, CaseReportArtifactKind.ImagePack);
    }

    private async Task Time(
        string name, IAssessmentReportRenderer renderer, AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind)
    {
        // Two renders first, so the fonts, the JIT and the native engine are warm.
        for (var warm = 0; warm < 2; warm++)
        {
            Assert.True((await renderer.RenderAsync(snapshot, kind)).PageCount > 0);
        }

        var render = new double[Renders];
        var open = new double[Renders];
        var allocated = new long[Renders];
        for (var index = 0; index < Renders; index++)
        {
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
            var started = Stopwatch.GetTimestamp();
            var artifact = await renderer.RenderAsync(snapshot, kind);
            render[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            allocated[index] = Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore);

            started = Stopwatch.GetTimestamp();
            using (var document = PdfDocument.Open(artifact.Pdf))
            {
                Assert.True(document.NumberOfPages > 0);
            }

            open[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        Array.Sort(render);
        Array.Sort(open);
        Array.Sort(allocated);
        output.WriteLine(
            "report-page-count {0}: renders={1}; render-ms-min={2:F1}; render-ms-p50={3:F1}; render-ms-p95={4:F1}; "
            + "pdfpig-open-ms-min={5:F1}; pdfpig-open-ms-p50={6:F1}; pdfpig-open-ms-p95={7:F1}; "
            + "process-allocated-bytes-p50={8}; process-allocated-bytes-p95={9}",
            name,
            Renders,
            render[0],
            Percentile(render, 0.50),
            Percentile(render, 0.95),
            open[0],
            Percentile(open, 0.50),
            Percentile(open, 0.95),
            Percentile(allocated, 0.50),
            Percentile(allocated, 0.95));
    }

    private static ReportImageEvidence[] Photos(AssessmentReportSnapshot snapshot, int count)
    {
        var photo = snapshot.Photos.Single();
        return Enumerable.Range(0, count)
            .Select(index => photo with
            {
                CustodyReference = $"site-{index}.jpg",
                Order = index,
                Role = index == 0 ? CaseAssetReportRole.Overview : CaseAssetReportRole.Supporting,
            })
            .ToArray();
    }

    private static T Percentile<T>(T[] sorted, double percentile) =>
        sorted[(int)Math.Ceiling(percentile * sorted.Length) - 1];

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
