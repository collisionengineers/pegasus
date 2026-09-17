using System.Security.Cryptography;
using Pegasus.Core.Reports;
using QuestPDF.Fluent;
using UglyToad.PdfPig;

namespace Pegasus.Infrastructure.Reports;

internal sealed class QuestPdfEstimateDocumentRenderer(ReportRenderGate gate) : IEstimateDocumentRenderer
{
    private const int MaximumPages = 20;
    private const int MaximumPdfBytes = 10 * 1024 * 1024;
    private readonly ReportRenderGate gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public string EngineVersion { get; } =
        $"QuestPDF/{typeof(QuestPDF.Settings).Assembly.GetName().Version}";

    public async Task<RenderedReportArtifact> RenderAsync(
        EstimateDocumentSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        ReportResources.RegisterFonts();
        var pdf = await gate.RunAsync(
            () => EstimateDocumentLayout.Compose(snapshot, ReportResources.Logo()).GeneratePdf(),
            cancellationToken).ConfigureAwait(false);
        if (pdf.Length > MaximumPdfBytes)
        {
            throw new ReportRenderRejectedException("The estimate document is too large to return.");
        }
        using var document = PdfDocument.Open(pdf);
        if (document.NumberOfPages > MaximumPages)
        {
            throw new ReportRenderRejectedException("The estimate document has too many pages to return.");
        }
        var parts = new[]
        {
            ReportChrome.Slug(snapshot.OurReference),
            ReportChrome.Slug(snapshot.EstimateName),
            "estimate.pdf",
        };
        var fileName = string.Join('_', parts);
        if (fileName.Length > 120)
        {
            fileName = fileName[..116] + ".pdf";
        }
        return new(
            fileName,
            pdf,
            document.NumberOfPages,
            Convert.ToHexStringLower(SHA256.HashData(pdf)),
            EstimateDocumentContract.TemplateVersion,
            EngineVersion);
    }
}
