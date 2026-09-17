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
    /// <summary>
    /// The printed photo square's longest edge in pixels: 91mm at 300 dpi
    /// needs 1075, so the page never prints a soft square and a 24-image
    /// report stays a few megabytes.
    /// </summary>
    private const int PhotoSquarePixels = 1200;
    private const int PhotoJpegQuality = 85;

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
        AssessmentReportRenderPolicy.RequireBoundedImages(snapshot.Photos);
        var feeNote = kind == CaseReportArtifactKind.FeeNote;
        var fileName = $"{ReportChrome.Slug(snapshot.OurReference)}_{(feeNote ? "fee_note" : "assessment")}.pdf";
        var pdf = await gate.RunAsync(() => Render(snapshot, kind), cancellationToken)
            .ConfigureAwait(false);
        return Artifact(fileName, pdf);
    }

    private static byte[] Render(AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind)
    {
        var images = kind == CaseReportArtifactKind.FeeNote
            ? new PreparedReportImages([], [], ReportResources.Logo())
            : new PreparedReportImages(
                snapshot.OrderedPhotos.Select(PreparePhoto).ToArray(),
                PrepareSignature(snapshot.Signatory),
                ReportResources.Logo());
        return AssessmentReportLayout.Compose(snapshot, kind, images).GeneratePdf();
    }

    private RenderedReportArtifact Artifact(string fileName, byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return new RenderedReportArtifact(
            fileName,
            pdf,
            document.NumberOfPages,
            Convert.ToHexStringLower(SHA256.HashData(pdf)),
            AssessmentReportContract.TemplateVersion,
            EngineVersion);
    }

    /// <summary>
    /// The square the page prints for one prepared image: the confirmed
    /// bytes decoded once, shown the way the crop editor showed them (EXIF
    /// orientation first, then the Engineer's whole-turn rotation), the
    /// persisted crop taken as fractions of that rotated source, and the
    /// centred square of the crop filling the frame. Bytes that do not
    /// decode fail the render closed: the report never prints a placeholder.
    /// </summary>
    private static byte[] PreparePhoto(ReportImageEvidence photo)
    {
        using var data = SKData.CreateCopy(photo.Content);
        using var codec = SKCodec.Create(data)
            ?? throw new ReportRenderRejectedException($"Report image '{photo.CustodyReference}' could not be decoded.");
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        var quarterTurn = photo.Rotation is CaseAssetRotation.Clockwise90 or CaseAssetRotation.Clockwise270;
        var crop = photo.AppliedCrop;

        // Decode no larger than the printed square needs from the cropped
        // region; the codec picks the nearest scale it supports.
        var displayedWidth = transposed ? codec.Info.Height : codec.Info.Width;
        var displayedHeight = transposed ? codec.Info.Width : codec.Info.Height;
        var rotatedWidth = quarterTurn ? displayedHeight : displayedWidth;
        var rotatedHeight = quarterTurn ? displayedWidth : displayedHeight;
        var croppedShortEdge = Math.Min((double)crop.Width * rotatedWidth, (double)crop.Height * rotatedHeight);
        var desiredScale = (float)Math.Min(1d, PhotoSquarePixels / Math.Max(1d, croppedShortEdge));
        var decodedSize = codec.GetScaledDimensions(desiredScale);
        using var decoded = SKBitmap.Decode(
            codec,
            new SKImageInfo(decodedSize.Width, decodedSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new ReportRenderRejectedException($"Report image '{photo.CustodyReference}' could not be decoded.");

        // The rotated source, exactly as the crop editor drew it.
        var shownWidth = transposed ? decoded.Height : decoded.Width;
        var shownHeight = transposed ? decoded.Width : decoded.Height;
        var sourceWidth = quarterTurn ? shownHeight : shownWidth;
        var sourceHeight = quarterTurn ? shownWidth : shownHeight;
        using var rotated = new SKBitmap(new SKImageInfo(sourceWidth, sourceHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.Translate(sourceWidth / 2f, sourceHeight / 2f);
            canvas.RotateDegrees((int)photo.Rotation);
            canvas.Translate(-shownWidth / 2f, -shownHeight / 2f);
            EncodedImageOrientation.Apply(canvas, origin, shownWidth, shownHeight);
            canvas.DrawBitmap(decoded, 0, 0);
        }

        // The crop as fractions of the rotated source, then its centred
        // square, which is what a square frame shows of it.
        var cropLeft = (float)crop.Left * sourceWidth;
        var cropTop = (float)crop.Top * sourceHeight;
        var cropWidth = Math.Max(1f, (float)crop.Width * sourceWidth);
        var cropHeight = Math.Max(1f, (float)crop.Height * sourceHeight);
        var side = Math.Min(cropWidth, cropHeight);
        var square = new SKRect(
            cropLeft + (cropWidth - side) / 2f,
            cropTop + (cropHeight - side) / 2f,
            cropLeft + (cropWidth - side) / 2f + side,
            cropTop + (cropHeight - side) / 2f + side);
        var outputSide = Math.Max(1, Math.Min(PhotoSquarePixels, (int)Math.Round(side)));
        using var output = new SKBitmap(new SKImageInfo(outputSide, outputSide, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(output))
        using (var source = SKImage.FromBitmap(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(
                source,
                square,
                new SKRect(0, 0, outputSide, outputSide),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        using var image = SKImage.FromBitmap(output);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, PhotoJpegQuality)
            ?? throw new ReportRenderRejectedException($"Report image '{photo.CustodyReference}' could not be prepared for print.");
        return encoded.ToArray();
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
