using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The report's logo (<c>docs/design/README.md</c>, "Logo"): the one governed master,
/// embedded as it is, and decoded once for the process rather than once per rendered
/// document.
/// </summary>
public sealed class ReportLogoTests(ITestOutputHelper output)
{
    /// <summary>The master's SHA-256, as <c>docs/design/README.md</c> pins it.</summary>
    private const string GovernedMasterSha256 = "E7247BE45911C46905343473E4C57B9F6ED7A450563D19C508C2D9652C2C63E2";

    private const int Renders = 15;

    [Fact]
    public void TheReportEmbedsTheGovernedMasterAndEveryDocumentSharesOneImage()
    {
        Assert.Equal(GovernedMasterSha256, Convert.ToHexString(SHA256.HashData(MasterBytes())));
        Assert.Same(ReportResources.Logo(), ReportResources.Logo());
    }

    /// <summary>
    /// Times a header-only render, the page's running header on an otherwise empty page, three
    /// ways: the logo handed over as bytes, so each document decodes the 3150 by 1756 master
    /// (the code before this change); the logo handed over as the shared image (the code now);
    /// and the production header itself, with its company block, on the shared image. It
    /// asserts that each renders a page and no speed: the figures are written to the test's
    /// output, and CI's run shows them.
    /// </summary>
    [Fact]
    public void TheHeaderOnlyRenderIsTimedWithTheLogoDecodedPerDocumentAndDecodedOnce()
    {
        // The host declares the QuestPDF licence and the embedded faces at composition.
        _ = new ServiceCollection().AddPegasusReportRendering();
        ReportResources.RegisterFonts();
        var master = MasterBytes();
        var shared = ReportResources.Logo();

        Time("logo-decoded-per-document", () => HeaderOnly(slot => slot
            .Image(master)
            .WithCompressionQuality(ImageCompressionQuality.VeryHigh)
            .FitWidth()));
        Time("logo-shared-image", () => HeaderOnly(slot => slot
            .Image(shared)
            .WithCompressionQuality(ImageCompressionQuality.VeryHigh)
            .FitWidth()));
        Time("production-header-shared-image", () => Document
            .Create(document => document.Page(page => ReportChrome.Page(
                page,
                shared,
                AssessmentReportWording.CompanyBlock(feeNote: false),
                "footer",
                ReportChrome.ReportBodyTop,
                column => column.Item().Height(1, Unit.Millimetre))))
            .GeneratePdf());
    }

    /// <summary>
    /// The running header's logo slot on an empty page, as the layout sets it: 44 mm wide,
    /// at the left, 8.2 mm down, between the header edges.
    /// </summary>
    private static byte[] HeaderOnly(Action<IContainer> logo) =>
        Document.Create(document => document.Page(page =>
        {
            page.Size(ReportChrome.PageWidth, ReportChrome.PageHeight, Unit.Millimetre);
            page.Margin(0);
            page.Header()
                .Height(ReportChrome.ReportBodyTop, Unit.Millimetre)
                .PaddingHorizontal(ReportChrome.HeaderEdge, Unit.Millimetre)
                .PaddingTop(8.2f, Unit.Millimetre)
                .Row(row => logo(row.RelativeItem().AlignLeft().Width(44, Unit.Millimetre)));
        })).GeneratePdf();

    private void Time(string name, Func<byte[]> render)
    {
        // Two renders first, so the fonts, the JIT and the native engine are warm for all three.
        for (var warm = 0; warm < 2; warm++)
        {
            AssertRenderedAPage(render());
        }

        var elapsed = new double[Renders];
        for (var index = 0; index < Renders; index++)
        {
            var started = Stopwatch.GetTimestamp();
            var pdf = render();
            elapsed[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            AssertRenderedAPage(pdf);
        }

        Array.Sort(elapsed);
        output.WriteLine(
            "report-header-render {0}: renders={1}; elapsed-ms-min={2:F1}; elapsed-ms-p50={3:F1}; elapsed-ms-p95={4:F1}",
            name,
            Renders,
            elapsed[0],
            elapsed[Renders / 2],
            elapsed[(int)Math.Ceiling(0.95 * Renders) - 1]);
    }

    private static void AssertRenderedAPage(byte[] pdf) =>
        Assert.True(pdf.AsSpan().StartsWith("%PDF-"u8), "The header-only render is not a PDF.");

    private static byte[] MasterBytes()
    {
        using var stream = typeof(QuestPdfAssessmentReportRenderer).Assembly.GetManifestResourceStream(
            "Pegasus.Infrastructure.Reports.Assets.brand.logo.png");
        Assert.NotNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
