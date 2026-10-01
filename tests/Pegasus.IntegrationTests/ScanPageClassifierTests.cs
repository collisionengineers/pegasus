using Pegasus.Infrastructure.Intake;
using SkiaSharp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The colour rule that tells a scanned document page from a photograph that
/// fills a PDF page. The rule is deliberately plain: paper is mostly white,
/// a photograph is not.
/// </summary>
public sealed class ScanPageClassifierTests
{
    [Fact]
    public void AWhitePageIsADocumentPage()
    {
        var classification = ScanPageClassifier.Classify(Png(120, 160, (_, _) => new SKColor(0xff, 0xff, 0xff)));

        Assert.Equal(ScanPageKind.Document, classification.Kind);
        Assert.NotNull(classification.NearWhiteFraction);
        Assert.True(classification.NearWhiteFraction >= 0.99);
    }

    [Fact]
    public void PrintedTextOnPaperIsStillADocumentPage()
    {
        // Every fourth row is a black text line: a dense page is still mostly paper.
        var classification = ScanPageClassifier.Classify(Png(120, 160, (_, y) => y % 4 == 0
            ? new SKColor(0x10, 0x10, 0x10)
            : new SKColor(0xfa, 0xfa, 0xf7)));

        Assert.Equal(ScanPageKind.Document, classification.Kind);
        Assert.InRange(classification.NearWhiteFraction!.Value, 0.70, 0.80);
    }

    [Fact]
    public void APhotographIsNotADocumentPage()
    {
        var random = new Random(7);
        var classification = ScanPageClassifier.Classify(Png(120, 160, (_, _) =>
            new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256))));

        Assert.Equal(ScanPageKind.Photograph, classification.Kind);
        Assert.True(classification.NearWhiteFraction < 0.05);
    }

    [Fact]
    public void ALargeRasterIsMeasuredOnAScaledCopy()
    {
        // 2480x3508 is A4 at 300 dpi. The measurement must not need the full
        // bitmap, and the share must survive the scaling.
        var classification = ScanPageClassifier.Classify(Png(2480, 3508, (x, _) => x < 1240
            ? new SKColor(0xff, 0xff, 0xff)
            : new SKColor(0x40, 0x40, 0x40)));

        Assert.InRange(classification.NearWhiteFraction!.Value, 0.45, 0.55);
    }

    [Fact]
    public void BytesThatDoNotDecodeAreADocumentPageWithNoMeasurement()
    {
        var classification = ScanPageClassifier.Classify([0x00, 0x01, 0x02, 0x03]);

        Assert.Equal(ScanPageKind.Document, classification.Kind);
        Assert.Null(classification.NearWhiteFraction);
    }

    [Fact]
    public void TheThresholdIsABoundary()
    {
        // The midpoint of the gap the corpus showed: documents at 0.76 and
        // above, full-page photographs at 0.04 and below.
        Assert.Equal(0.40, ScanPageClassifier.PaperWhiteFraction);
        var atThreshold = ScanPageClassifier.Classify(Png(100, 100, (x, _) => x < 40
            ? new SKColor(0xff, 0xff, 0xff)
            : new SKColor(0x00, 0x00, 0x00)));
        var belowThreshold = ScanPageClassifier.Classify(Png(100, 100, (x, _) => x < 39
            ? new SKColor(0xff, 0xff, 0xff)
            : new SKColor(0x00, 0x00, 0x00)));

        Assert.Equal(ScanPageKind.Document, atThreshold.Kind);
        Assert.Equal(ScanPageKind.Photograph, belowThreshold.Kind);
    }

    private static byte[] Png(int width, int height, Func<int, int, SKColor> pixel)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, pixel(x, y));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
