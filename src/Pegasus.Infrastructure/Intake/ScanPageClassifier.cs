using Pegasus.Core.Intake;
using SkiaSharp;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// What a full-page raster is: the scan of a document page, or a photograph
/// that fills the page.
/// </summary>
internal enum ScanPageKind
{
    /// <summary>A scanned document page. It yields text through OCR, never an image.</summary>
    Document,

    /// <summary>A photograph that happens to fill the page. It is an ordinary embedded image.</summary>
    Photograph
}

/// <param name="Kind">The page kind.</param>
/// <param name="NearWhiteFraction">
/// The share of sampled pixels that are paper-white, or null when the raster
/// could not be decoded and the page was treated as a document by default.
/// </param>
internal readonly record struct ScanPageClassification(ScanPageKind Kind, double? NearWhiteFraction);

/// <summary>
/// Tells a scanned document page from a photograph that fills a PDF page. Both
/// look the same to the page geometry (one raster covering the page and almost
/// no embedded text), so the raster's colour decides: paper is mostly white,
/// a photograph is not. The raster is decoded small, so the cost is one scaled
/// decode per full-page raster and nothing else.
/// </summary>
internal static class ScanPageClassifier
{
    /// <summary>Named in the reader version so a reading records which rule read it.</summary>
    public const string Version = "scan-page-classifier-1";

    /// <summary>
    /// The near-white share at or above which a full-page raster is a document
    /// page. Calibrated on the local corpus (artifacts/0110-scan-page-classifier,
    /// ADR-0061): every scanned document page measured 0.76 or more, every
    /// vehicle photograph filling a page 0.04 or less, and nothing fell between.
    /// The threshold is the midpoint of that gap, so each kind has the same
    /// margin for a darker scan or a brighter photograph.
    /// </summary>
    public const double PaperWhiteFraction = 0.40;

    /// <summary>A channel at or above this is "paper": the white of a scan, not the grey of a photograph.</summary>
    public const byte NearWhiteChannel = 216;

    private const int SampleLongestSide = 128;

    /// <summary>The same declared-pixel bound the VRM engine refuses above.</summary>
    private const long MaximumDeclaredPixels = 100_000_000;

    /// <summary>
    /// Classifies one full-page raster from its encoded bytes (JPEG or PNG, as
    /// the PDF reader extracts them). A raster that cannot be decoded is a
    /// document page: it goes to OCR and never to a gallery.
    /// </summary>
    public static ScanPageClassification Classify(ReadOnlySpan<byte> encodedImage)
    {
        var fraction = NearWhiteFraction(encodedImage);
        if (fraction is not { } measured)
        {
            return new(ScanPageKind.Document, null);
        }

        return new(measured >= PaperWhiteFraction ? ScanPageKind.Document : ScanPageKind.Photograph, measured);
    }

    /// <summary>
    /// The share of pixels whose red, green and blue are all at least
    /// <see cref="NearWhiteChannel"/>, measured on a copy scaled to at most
    /// <see cref="SampleLongestSide"/> pixels on its longest side. Null when the
    /// bytes do not decode.
    /// </summary>
    internal static double? NearWhiteFraction(ReadOnlySpan<byte> encodedImage)
    {
        if (encodedImage.IsEmpty)
        {
            return null;
        }

        try
        {
            using var data = SKData.CreateCopy(encodedImage);
            using var codec = SKCodec.Create(data);
            if (codec is null)
            {
                return null;
            }

            var info = codec.Info;
            if ((long)info.Width * info.Height is <= 0 or > MaximumDeclaredPixels)
            {
                return null;
            }

            var scale = Math.Min(1f, (float)SampleLongestSide / Math.Max(info.Width, info.Height));
            var scaled = codec.GetScaledDimensions(scale);
            var target = new SKImageInfo(
                Math.Max(1, scaled.Width),
                Math.Max(1, scaled.Height),
                SKColorType.Rgba8888,
                SKAlphaType.Premul);
            using var bitmap = SKBitmap.Decode(codec, target);
            if (bitmap is null)
            {
                return null;
            }

            var pixels = bitmap.Pixels;
            if (pixels.Length == 0)
            {
                return null;
            }

            var nearWhite = 0;
            foreach (var pixel in pixels)
            {
                if (pixel.Red >= NearWhiteChannel && pixel.Green >= NearWhiteChannel && pixel.Blue >= NearWhiteChannel)
                {
                    nearWhite++;
                }
            }

            return (double)nearWhite / pixels.Length;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return null;
        }
    }
}
