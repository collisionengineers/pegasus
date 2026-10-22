using Pegasus.Core.Documents;
using PDFtoImage;
using SkiaSharp;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Draws one PDF page as an image with PDFium, through PDFtoImage (ADR-0081).
/// The PDF reader calls it only for a full-page raster it cannot hand over
/// itself (R194); it owns no policy, so what the page is stays the
/// classifier's call and what is kept stays the reader's.
/// </summary>
internal static class PdfPageRasteriser
{
    /// <summary>Named in the reader version so a reading records which engine drew the page.</summary>
    public const string Version = "pdftoimage-5.2.1;pdfium-147.0.7690";

    /// <summary>
    /// The page drawn over paper white as an uncompressed PNG, or null when
    /// nothing on the page draws at all: a raster PDFium cannot decode either
    /// draws nothing, and an empty drawing proves nothing about the page. The
    /// PNG only carries the small drawing to the classifier, so it is stored
    /// rather than compressed, which costs a fraction of the default encode.
    /// </summary>
    public static byte[]? RenderSample(ReadOnlyMemory<byte> pdf, int pageIndex, int width, int height)
    {
        using var bitmap = Draw(pdf, pageIndex, width, height, SKColors.Transparent);
        if (bitmap is null || !AnyPixelDrawn(bitmap))
        {
            return null;
        }

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.DrawColor(SKColors.White, SKBlendMode.DstOver);
        }

        using var pixels = bitmap.PeekPixels();
        using var png = pixels.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.NoFilters, 0));
        return png?.ToArray();
    }

    /// <summary>The page drawn over paper white and encoded as a JPEG, or null when it does not draw.</summary>
    public static byte[]? RenderJpeg(ReadOnlyMemory<byte> pdf, int pageIndex, int width, int height, int quality)
    {
        using var bitmap = Draw(pdf, pageIndex, width, height, SKColors.White);
        using var jpeg = bitmap?.Encode(SKEncodedImageFormat.Jpeg, quality);
        return jpeg?.ToArray();
    }

    /// <summary>
    /// PDFium ships for the two runtimes the hosts publish; elsewhere nothing
    /// is drawn and the page keeps the reader's own path.
    /// </summary>
    private static SKBitmap? Draw(ReadOnlyMemory<byte> pdf, int pageIndex, int width, int height, SKColor background)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return null;
        }

        using var stream = DocumentContentCachePublisherExtensions.StreamOf(pdf);
        return Conversion.ToImage(
            stream,
            pageIndex,
            leaveOpen: true,
            options: new RenderOptions(Width: width, Height: height, BackgroundColor: background));
    }

    private static bool AnyPixelDrawn(SKBitmap bitmap)
    {
        // Alpha is the last byte of each pixel in both 32-bit layouts.
        var pixels = bitmap.GetPixelSpan();
        for (var alpha = bitmap.BytesPerPixel - 1; alpha < pixels.Length; alpha += bitmap.BytesPerPixel)
        {
            if (pixels[alpha] != 0)
            {
                return true;
            }
        }

        return false;
    }
}
