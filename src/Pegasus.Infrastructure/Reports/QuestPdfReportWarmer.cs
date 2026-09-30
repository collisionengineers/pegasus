using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using UglyToad.PdfPig;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The <see cref="IWarmReportRenderer"/> adapter. The first render in a process
/// registers the embedded faces and starts the layout engine, which took 2.4 to
/// 7 s inside a staff request. This does the same work with a one-line page in
/// the report's typeface and no image, and reads the page count back as a real
/// render does, then drops the bytes. It runs through the one
/// <see cref="ReportRenderGate"/>, so a real render that arrives meanwhile
/// waits for it, and it can never be admitted beside another render.
/// </summary>
internal sealed class QuestPdfReportWarmer(ReportRenderGate gate) : IWarmReportRenderer
{
    private readonly ReportRenderGate gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public async Task WarmAsync(CancellationToken cancellationToken = default) =>
        _ = await gate.RunAsync(_ => Task.FromResult(RenderOneLine()), cancellationToken).ConfigureAwait(false);

    private static byte[] RenderOneLine()
    {
        ReportResources.RegisterFonts();
        var pdf = Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.DefaultTextStyle(style => style.FontFamily(ReportChrome.FontFamily));
            page.Content().Text("Warm-up");
        })).GeneratePdf();
        using var opened = PdfDocument.Open(pdf);
        return opened.NumberOfPages > 0
            ? pdf
            : throw new InvalidOperationException("The warm-up page did not render.");
    }
}
