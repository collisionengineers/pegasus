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
/// </summary>
internal sealed class PlateDetector(InferenceSession session)
{
    private const int InputSize = 384;
    private const byte PaddingGrey = 114;

    public IReadOnlyList<DetectedPlate> Detect(SKBitmap image, double scoreThreshold)
    {
        var ratio = Math.Min(
            (float)InputSize / image.Width,
            (float)InputSize / image.Height);
        var scaledWidth = Math.Max(1, (int)Math.Round(image.Width * ratio));
        var scaledHeight = Math.Max(1, (int)Math.Round(image.Height * ratio));
        var padLeft = (InputSize - scaledWidth) / 2;
        var padTop = (InputSize - scaledHeight) / 2;

        using var letterboxed = new SKBitmap(
            new SKImageInfo(InputSize, InputSize, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(letterboxed))
        {
            canvas.Clear(new SKColor(PaddingGrey, PaddingGrey, PaddingGrey));
            using var resized = image.Resize(
                new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Opaque),
                new SKSamplingOptions(SKFilterMode.Linear));
            if (resized is null)
            {
                return [];
            }

            canvas.DrawBitmap(resized, padLeft, padTop);
        }

        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
        var pixels = letterboxed.Pixels;
        for (var y = 0; y < InputSize; y++)
        {
            var row = y * InputSize;
            for (var x = 0; x < InputSize; x++)
            {
                var pixel = pixels[row + x];
                tensor[0, 0, y, x] = pixel.Red / 255f;
                tensor[0, 1, y, x] = pixel.Green / 255f;
                tensor[0, 2, y, x] = pixel.Blue / 255f;
            }
        }

        var inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(
            [NamedOnnxValue.CreateFromTensor(inputName, tensor)]);
        var output = results[0].AsTensor<float>();
        return Decode(output, ratio, padLeft, padTop, image.Width, image.Height, scoreThreshold);
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
