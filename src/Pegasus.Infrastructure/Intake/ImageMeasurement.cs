using Pegasus.Core.Intake;
using SkiaSharp;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// An encoded image's pixel size and its difference-hash fingerprint
/// (<see cref="KnownGraphics"/>).
/// </summary>
internal readonly record struct ImageMeasure(int Width, int Height, ulong Fingerprint);

/// <summary>
/// Measures every JPEG and PNG a reading yields, so the photograph rule
/// (<see cref="InstructionEvidenceImages"/>) judges an attachment, an inline
/// image and an embedded image on the same facts. The size comes from the
/// encoded header; the fingerprint from one small scaled decode.
/// </summary>
internal static class ImageMeasurement
{
    /// <summary>Named in the reader version so a reading records which rule measured it.</summary>
    public const string Version = "image-measure-1";

    /// <summary>The same declared-pixel bound the scan-page classifier refuses above.</summary>
    private const long MaximumDeclaredPixels = 100_000_000;

    /// <summary>
    /// Decoding at least this many pixels a side keeps the 9x8 average
    /// stable while letting a large JPEG decode at a fraction of its size.
    /// </summary>
    private const int SampleShortestSide = 72;

    /// <summary>
    /// The candidates with every image's size and fingerprint recorded. A
    /// size the PDF reader already took from the image object stands; one
    /// image placed on many pages shares one buffer and is measured once.
    /// </summary>
    public static IReadOnlyList<IntakeAssetCandidate> Complete(IReadOnlyList<IntakeAssetCandidate> candidates)
    {
        var measured = new Dictionary<ReadOnlyMemory<byte>, ImageMeasure?>();
        var completed = new IntakeAssetCandidate[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!InstructionEvidenceImages.IsImage(candidate.MediaType))
            {
                completed[index] = candidate;
                continue;
            }

            if (!measured.TryGetValue(candidate.Content, out var measure))
            {
                measure = Measure(candidate.Content.Span);
                measured[candidate.Content] = measure;
            }

            completed[index] = measure is { } known
                ? candidate with
                {
                    WidthPixels = candidate.WidthPixels ?? known.Width,
                    HeightPixels = candidate.HeightPixels ?? known.Height,
                    Fingerprint = known.Fingerprint
                }
                : candidate;
        }

        return completed;
    }

    /// <summary>
    /// The image's size and fingerprint, or null when the bytes do not
    /// decode. The fingerprint is a 64-bit difference hash: the image is
    /// reduced to a 9x8 grey grid by averaging, and each bit says whether a
    /// cell is brighter than its right-hand neighbour. Transparent pixels
    /// read as white paper. Re-encoding and resizing barely move it, so one
    /// logo saved many ways keeps nearly the same fingerprint.
    /// </summary>
    internal static ImageMeasure? Measure(ReadOnlySpan<byte> encodedImage)
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

            var scale = Math.Min(1f, (float)SampleShortestSide / Math.Min(info.Width, info.Height));
            var scaled = codec.GetScaledDimensions(scale);
            var target = new SKImageInfo(
                Math.Max(1, scaled.Width),
                Math.Max(1, scaled.Height),
                SKColorType.Rgba8888,
                SKAlphaType.Unpremul);
            using var bitmap = SKBitmap.Decode(codec, target);
            if (bitmap is null)
            {
                return null;
            }

            return new(info.Width, info.Height, DifferenceHash(bitmap.Pixels, bitmap.Width, bitmap.Height));
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return null;
        }
    }

    private static ulong DifferenceHash(SKColor[] pixels, int width, int height)
    {
        const int columns = 9;
        const int rows = 8;
        var sums = new double[rows * columns];
        var counts = new int[rows * columns];
        for (var y = 0; y < height; y++)
        {
            var row = y * rows / height;
            for (var x = 0; x < width; x++)
            {
                var pixel = pixels[(y * width) + x];
                var alpha = pixel.Alpha / 255.0;
                var grey = (0.299 * pixel.Red) + (0.587 * pixel.Green) + (0.114 * pixel.Blue);
                var cell = (row * columns) + (x * columns / width);
                sums[cell] += (alpha * grey) + ((1 - alpha) * 255);
                counts[cell]++;
            }
        }

        ulong hash = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns - 1; column++)
            {
                var left = row * columns + column;
                hash = (hash << 1)
                    | (Mean(sums, counts, left) > Mean(sums, counts, left + 1) ? 1UL : 0UL);
            }
        }

        return hash;
    }

    private static double Mean(double[] sums, int[] counts, int cell) =>
        counts[cell] == 0 ? 0 : sums[cell] / counts[cell];
}
