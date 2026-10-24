using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Pegasus.Infrastructure.Vision;

internal sealed record DetectedPlate(
    float Left,
    float Top,
    float Right,
    float Bottom,
    float Score);

/// <summary>
/// YOLOv9-t 384 end-to-end plate detection: letterboxed RGB float input
/// (0-1, grey-114 padding), post-NMS output rows of
/// `[batch, x1, y1, x2, y2, class, score]`, rescaled back to source pixels.
/// The letterbox is laid out from the source's own size, so the bitmap handed
/// in may be a smaller decode of that source: it is resized to the same
/// 384-pixel input and the boxes come back in the source's pixels either way.
/// </summary>
internal sealed class PlateDetector(InferenceSession session)
{
    internal const int InputSize = 384;
    private const byte PaddingGrey = 114;

    /// <param name="image">The source, or a decode of it no smaller than the input size on its longest side.</param>
    /// <param name="sourceWidth">The source's width in pixels, which the boxes are returned in.</param>
    /// <param name="sourceHeight">The source's height in pixels.</param>
    public IReadOnlyList<DetectedPlate> Detect(
        SKBitmap image,
        int sourceWidth,
        int sourceHeight,
        double scoreThreshold)
    {
        var ratio = Math.Min(
            (float)InputSize / sourceWidth,
            (float)InputSize / sourceHeight);
        var scaledWidth = Math.Max(1, (int)Math.Round(sourceWidth * ratio));
        var scaledHeight = Math.Max(1, (int)Math.Round(sourceHeight * ratio));
        var padLeft = (InputSize - scaledWidth) / 2;
        var padTop = (InputSize - scaledHeight) / 2;

        using var letterboxed = Letterbox(image, scaledWidth, scaledHeight, padLeft, padTop);
        if (letterboxed is null)
        {
            return [];
        }

        var tensor = ToInputTensor(letterboxed);
        var inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(
            [NamedOnnxValue.CreateFromTensor(inputName, tensor)]);
        var output = results[0].AsTensor<float>();
        return Decode(output, ratio, padLeft, padTop, sourceWidth, sourceHeight, scoreThreshold);
    }

    /// <summary>The model's square input: the image resized to the scaled size on grey padding.</summary>
    internal static SKBitmap? Letterbox(SKBitmap image, int scaledWidth, int scaledHeight, int padLeft, int padTop)
    {
        var letterboxed = new SKBitmap(
            new SKImageInfo(InputSize, InputSize, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(letterboxed))
        {
            canvas.Clear(new SKColor(PaddingGrey, PaddingGrey, PaddingGrey));
            using var resized = image.Resize(
                new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Opaque),
                new SKSamplingOptions(SKFilterMode.Linear));
            if (resized is null)
            {
                letterboxed.Dispose();
                return null;
            }

            canvas.DrawBitmap(resized, padLeft, padTop);
        }

        return letterboxed;
    }

    /// <summary>
    /// The letterboxed bitmap as the model's planar input, `[1, 3, size, size]`,
    /// red plane then green then blue, each byte scaled to 0-1. The bitmap is
    /// opaque Rgba8888, so the bytes in memory are the channel values and the
    /// tensor is filled from them directly.
    /// </summary>
    internal static DenseTensor<float> ToInputTensor(SKBitmap letterboxed)
    {
        if (letterboxed.ColorType != SKColorType.Rgba8888
            || letterboxed.Width != InputSize
            || letterboxed.Height != InputSize)
        {
            throw new ArgumentException(
                $"The detector input is a {InputSize}x{InputSize} Rgba8888 bitmap.", nameof(letterboxed));
        }

        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
        var pixels = letterboxed.GetPixelSpan();
        var stride = letterboxed.RowBytes;
        var planes = tensor.Buffer.Span;
        var plane = InputSize * InputSize;
        for (var y = 0; y < InputSize; y++)
        {
            var source = pixels.Slice(y * stride, InputSize * 4);
            var row = y * InputSize;
            for (var x = 0; x < InputSize; x++)
            {
                var at = x * 4;
                planes[row + x] = source[at] / 255f;
                planes[plane + row + x] = source[at + 1] / 255f;
                planes[2 * plane + row + x] = source[at + 2] / 255f;
            }
        }

        return tensor;
    }

    private static DetectedPlate[] Decode(
        Tensor<float> output,
        float ratio,
        float padLeft,
        float padTop,
        int sourceWidth,
        int sourceHeight,
        double scoreThreshold)
    {
        var dimensions = output.Dimensions;
        var rank = dimensions.Length;
        int rows;
        int columns;
        var hasBatchColumn = false;
        if (rank == 3)
        {
            rows = dimensions[1];
            columns = dimensions[2];
        }
        else if (rank == 2)
        {
            rows = dimensions[0];
            columns = dimensions[1];
            hasBatchColumn = columns >= 7;
        }
        else
        {
            return [];
        }

        var offset = hasBatchColumn ? 1 : 0;
        if (columns < offset + 6)
        {
            return [];
        }

        var plates = new List<DetectedPlate>();
        for (var row = 0; row < rows; row++)
        {
            var currentRow = row;
            float Value(int column) => rank == 3
                ? output[0, currentRow, column]
                : output[currentRow, column];

            var score = Value(offset + 5);
            if (score < scoreThreshold)
            {
                continue;
            }

            var left = Math.Clamp((Value(offset) - padLeft) / ratio, 0, sourceWidth - 1);
            var top = Math.Clamp((Value(offset + 1) - padTop) / ratio, 0, sourceHeight - 1);
            var right = Math.Clamp((Value(offset + 2) - padLeft) / ratio, 0, sourceWidth - 1);
            var bottom = Math.Clamp((Value(offset + 3) - padTop) / ratio, 0, sourceHeight - 1);
            if (right - left < 2 || bottom - top < 2)
            {
                continue;
            }

            plates.Add(new DetectedPlate(left, top, right, bottom, score));
        }

        return plates
            .OrderByDescending(plate => plate.Score)
            .ToArray();
    }
}
