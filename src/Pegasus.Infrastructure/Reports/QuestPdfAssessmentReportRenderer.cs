using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Custody;
using QuestPDF.Fluent;
using SkiaSharp;
using UglyToad.PdfPig;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The one <see cref="IAssessmentReportRenderer"/> adapter (ADR-0050): lays
/// the accepted snapshot out with QuestPDF on embedded fonts, returns the
/// bytes with their SHA-256, page count, template and engine versions, and
/// fails closed on anything the snapshot does not supply in a printable form.
/// </summary>
internal sealed class QuestPdfAssessmentReportRenderer(ReportRenderGate gate) : IAssessmentReportRenderer
{
    private readonly ReportRenderGate gate = gate ?? throw new ArgumentNullException(nameof(gate));

    public string EngineVersion { get; } =
        $"QuestPDF/{typeof(QuestPDF.Settings).Assembly.GetName().Version}";

    public async Task<RenderedReportArtifact> RenderAsync(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ReportResources.RegisterFonts();
        var suffix = kind switch
        {
            CaseReportArtifactKind.FeeNote => "fee_note",
            CaseReportArtifactKind.ImagePack => "images",
            CaseReportArtifactKind.AssessmentReport => "report",
            _ => throw new ReportRenderRejectedException($"Unsupported report artifact kind '{kind}'."),
        };
        var fileName = $"{ReportChrome.Slug(snapshot.OurReference)}_{suffix}.pdf";
        var pdf = await gate.RunAsync(token => RenderPdfAsync(snapshot, kind, token), cancellationToken)
            .ConfigureAwait(false);
        return Artifact(fileName, pdf);
    }

    /// <summary>
    /// Prepares the images, one source image in memory at a time, then lays
    /// the document out. The token is the gate's: cancelled when the caller
    /// stops waiting, and checked between images so an abandoned render stops
    /// instead of running on.
    /// </summary>
    private static async Task<byte[]> RenderPdfAsync(
        AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind, CancellationToken cancellationToken)
    {
        PreparedReportImages images;
        using (DocumentReadTelemetry.Start("report.photos.prepare"))
        {
            images = await PrepareAsync(snapshot, kind, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var document = AssessmentReportLayout.Compose(snapshot, kind, images);
        using var generating = DocumentReadTelemetry.Start("report.pdf.generate");
        return document.GeneratePdf();
    }

    /// <summary>
    /// The images one artifact prints. The report leads page 1 with the
    /// Overview and prints it nowhere else (operator, 7 October 2026); the
    /// image pack prints every image in the grid. Full page has no effect on
    /// the Overview. Each image is opened, prepared and let go of before the
    /// next is opened.
    /// </summary>
    private static async Task<PreparedReportImages> PrepareAsync(
        AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind, CancellationToken cancellationToken)
    {
        if (kind == CaseReportArtifactKind.FeeNote)
        {
            return new(null, [], [], ReportResources.Logo());
        }
        var ordered = snapshot.OrderedPhotos;
        var overview = ordered.FirstOrDefault(photo => photo.Role == CaseAssetReportRole.Overview);
        var lead = kind == CaseReportArtifactKind.AssessmentReport ? overview : null;
        var leadImage = lead is null
            ? null
            : await PreparePhotoAsync(lead, ReportChrome.LeadSlotHeight, cancellationToken).ConfigureAwait(false);
        var photos = new List<PreparedReportPhoto>(ordered.Count);
        foreach (var photo in ordered)
        {
            if (ReferenceEquals(photo, lead))
            {
                continue;
            }
            var fullPage = photo.FullPage && !ReferenceEquals(photo, overview);
            photos.Add(new PreparedReportPhoto(
                await PreparePhotoAsync(photo, null, cancellationToken).ConfigureAwait(false),
                fullPage));
        }
        return new(leadImage, photos, PrepareSignature(snapshot.Signatory), ReportResources.Logo());
    }

    private static async Task<byte[]> PreparePhotoAsync(
        ReportImageEvidence photo, float? slotHeight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = await photo.OpenAsync(cancellationToken).ConfigureAwait(false);
        return ReportPhotoPreparation.Prepare(
            content, photo.CustodyReference, photo.Rotation, photo.AppliedCrop, slotHeight, cancellationToken);
    }

    private RenderedReportArtifact Artifact(string fileName, byte[] pdf)
    {
        int pageCount;
        using (DocumentReadTelemetry.Start("report.pdf.pagecount"))
        {
            using var document = PdfDocument.Open(pdf);
            pageCount = document.NumberOfPages;
        }

        return new RenderedReportArtifact(
            fileName,
            pdf,
            pageCount,
            Convert.ToHexStringLower(SHA256.HashData(pdf)),
            AssessmentReportContract.TemplateVersion,
            EngineVersion);
    }

    /// <summary>
    /// The signature as the page prints it: decoded once, EXIF orientation
    /// applied, re-encoded losslessly so any accepted source format prints
    /// the same way and undecodable bytes fail closed.
    /// </summary>
    private static byte[] PrepareSignature(ReportSignatory signatory)
    {
        using var data = SKData.CreateCopy(signatory.SignatureContent);
        using var codec = SKCodec.Create(data)
            ?? throw new ReportRenderRejectedException("The report signature image could not be decoded.");
        using var decoded = SKBitmap.Decode(codec)
            ?? throw new ReportRenderRejectedException("The report signature image could not be decoded.");
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        var width = transposed ? decoded.Height : decoded.Width;
        var height = transposed ? decoded.Width : decoded.Height;
        using var shown = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(shown))
        {
            canvas.Clear(SKColors.Transparent);
            EncodedImageOrientation.Apply(canvas, origin, width, height);
            canvas.DrawBitmap(decoded, 0, 0);
        }
        using var image = SKImage.FromBitmap(shown);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new ReportRenderRejectedException("The report signature image could not be prepared for print.");
        return encoded.ToArray();
    }

}
