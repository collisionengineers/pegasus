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
    /// A slot's width in pixels: 80.4 mm at 300 dpi needs 950, so neither
    /// the grid nor page 1 prints a soft image, and each embeds at most 1000
    /// px across whatever the source size.
    /// </summary>
    private const int SlotPixels = 1000;

    /// <summary>The longest edge of an image that prints on a page of its own.</summary>
    private const int FullPagePixels = 2000;
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
        var suffix = kind switch
        {
            CaseReportArtifactKind.FeeNote => "fee_note",
            CaseReportArtifactKind.ImagePack => "images",
            CaseReportArtifactKind.AssessmentReport => "assessment",
            _ => throw new ReportRenderRejectedException($"Unsupported report artifact kind '{kind}'."),
        };
        var fileName = $"{ReportChrome.Slug(snapshot.OurReference)}_{suffix}.pdf";
        var pdf = await gate.RunAsync(() => Render(snapshot, kind), cancellationToken)
            .ConfigureAwait(false);
        return Artifact(fileName, pdf);
    }

    private static byte[] Render(AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind) =>
        AssessmentReportLayout.Compose(snapshot, kind, Prepare(snapshot, kind)).GeneratePdf();

    /// <summary>
    /// The images one artifact prints. The report leads page 1 with the
    /// Close-up and prints it nowhere else; the image pack prints every
    /// image in the grid. Full page has no effect on the Close-up.
    /// </summary>
    private static PreparedReportImages Prepare(AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind)
    {
        if (kind == CaseReportArtifactKind.FeeNote)
        {
            return new(null, [], [], ReportResources.Logo());
        }
        var ordered = snapshot.OrderedPhotos;
        var closeUp = ordered.FirstOrDefault(photo => photo.Role == CaseAssetReportRole.CloseUp);
        var lead = kind == CaseReportArtifactKind.AssessmentReport ? closeUp : null;
        var photos = ordered
            .Where(photo => !ReferenceEquals(photo, lead))
            .Select(photo =>
            {
                var fullPage = photo.FullPage && !ReferenceEquals(photo, closeUp);
                return new PreparedReportPhoto(
                    PreparePhoto(photo, fullPage ? null : ReportChrome.GridSlotHeight),
                    fullPage);
            })
            .ToArray();
        return new(
            lead is null ? null : PreparePhoto(lead, ReportChrome.LeadSlotHeight),
            photos,
            PrepareSignature(snapshot.Signatory),
            ReportResources.Logo());
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
    /// What the page prints for one prepared image: the confirmed bytes
    /// decoded once, shown the way the crop editor showed them (EXIF
    /// orientation first, then the Engineer's whole-turn rotation), and the
    /// persisted crop taken as fractions of that rotated source. An image
    /// for a slot is then trimmed about its centre to the slot's shape, so it
    /// fills the slot; an image for a page of its own keeps its whole crop.
    /// Neither is ever enlarged. Bytes that do not decode fail the render
    /// closed: the report never prints a placeholder.
    /// </summary>
    private static byte[] PreparePhoto(ReportImageEvidence photo, float? slotHeight)
    {
        using var data = SKData.CreateCopy(photo.Content);
        using var codec = SKCodec.Create(data)
            ?? throw new ReportRenderRejectedException($"Report image '{photo.CustodyReference}' could not be decoded.");
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        var quarterTurn = photo.Rotation is CaseAssetRotation.Clockwise90 or CaseAssetRotation.Clockwise270;
        var crop = photo.AppliedCrop;
        var shape = slotHeight is { } height ? ReportChrome.SlotWidth / height : (float?)null;

        // Decode no larger than the print needs from the part it keeps. The
        // codec picks the nearest scale it supports, and the next one up when
        // that would leave the print short of its pixels.
        var displayedWidth = transposed ? codec.Info.Height : codec.Info.Width;
        var displayedHeight = transposed ? codec.Info.Width : codec.Info.Height;
        var rotatedWidth = quarterTurn ? displayedHeight : displayedWidth;
        var rotatedHeight = quarterTurn ? displayedWidth : displayedHeight;
        var wanted = Kept((float)crop.Width * rotatedWidth, (float)crop.Height * rotatedHeight, shape);
        var decodeScale = Scale(wanted, shape);
        var decodedSize = codec.GetScaledDimensions(decodeScale);
        if (decodedSize.Width < codec.Info.Width * decodeScale)
        {
            decodedSize = codec.GetScaledDimensions(Math.Min(1f, decodeScale + 0.125f));
        }
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

        // The crop as fractions of the rotated source, then the part of it
        // the print keeps, about the crop's centre.
        var cropLeft = (float)crop.Left * sourceWidth;
        var cropTop = (float)crop.Top * sourceHeight;
        var cropWidth = Math.Max(1f, (float)crop.Width * sourceWidth);
        var cropHeight = Math.Max(1f, (float)crop.Height * sourceHeight);
        var kept = Kept(cropWidth, cropHeight, shape);
        var left = cropLeft + (cropWidth - kept.Width) / 2f;
        var top = cropTop + (cropHeight - kept.Height) / 2f;
        var scale = Scale(kept, shape);
        var outputWidth = Math.Max(1, (int)Math.Round(kept.Width * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(kept.Height * scale));
        using var output = new SKBitmap(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(output))
        using (var source = SKImage.FromBitmap(rotated))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawImage(
                source,
                new SKRect(left, top, left + kept.Width, top + kept.Height),
                new SKRect(0, 0, outputWidth, outputHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        using var image = SKImage.FromBitmap(output);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, PhotoJpegQuality)
            ?? throw new ReportRenderRejectedException($"Report image '{photo.CustodyReference}' could not be prepared for print.");
        return encoded.ToArray();
    }

    /// <summary>
    /// The part of a crop the print keeps: all of it, or for a slot the
    /// largest part of the slot's shape, width over height.
    /// </summary>
    private static SKSize Kept(float cropWidth, float cropHeight, float? shape)
    {
        if (shape is not { } widthOverHeight)
        {
            return new(cropWidth, cropHeight);
        }
        return cropWidth / cropHeight > widthOverHeight
            ? new(cropHeight * widthOverHeight, cropHeight)
            : new(cropWidth, cropWidth / widthOverHeight);
    }

    /// <summary>What brings the kept part within its pixel budget, never more than one.</summary>
    private static float Scale(SKSize kept, float? shape) => shape is null
        ? Math.Min(1f, FullPagePixels / Math.Max(1f, Math.Max(kept.Width, kept.Height)))
        : Math.Min(1f, SlotPixels / Math.Max(1f, kept.Width));

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
