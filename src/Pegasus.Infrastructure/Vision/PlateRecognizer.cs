using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace Pegasus.Infrastructure.Vision;

internal sealed record RecognizedPlate(string Text, double Confidence);

/// <summary>
/// The fast-plate-ocr global CCT (small, v2) recogniser: RGB uint8
/// `[1, 64, 128, 3]` input, per-slot character probabilities out. Decoding is
/// per-slot argmax; trailing pad characters are stripped and any interior pad
/// abstains rather than guessing. The reported confidence is the lowest kept
/// per-character probability — the weakest link, chosen conservatively
/// pending open decision 1.
/// </summary>
internal sealed class PlateRecognizer(InferenceSession session)
{
    private const int InputHeight = 64;
    private const int InputWidth = 128;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_";
    private const char PadCharacter = '_';

    public RecognizedPlate? Recognize(SKBitmap plateCrop)
    {
        using var resized = plateCrop.Resize(
            new SKImageInfo(InputWidth, InputHeight, SKColorType.Rgba8888, SKAlphaType.Opaque),
            new SKSamplingOptions(SKFilterMode.Linear));
        if (resized is null)
        {
            return null;
        }

        var tensor = ToInputTensor(resized);
        var inputName = session.InputMetadata.Keys.First();
        using var results = session.Run(
            [NamedOnnxValue.CreateFromTensor(inputName, tensor)]);
        var output = results[0].AsTensor<float>();
        var dimensions = output.Dimensions;
        if (dimensions.Length != 3 || dimensions[2] != Alphabet.Length)
        {
            return null;
        }

        var slots = dimensions[1];
        var characters = new char[slots];
        var probabilities = new double[slots];
        for (var slot = 0; slot < slots; slot++)
        {
            var bestIndex = 0;
            var bestProbability = float.MinValue;
            for (var index = 0; index < Alphabet.Length; index++)
            {
                var probability = output[0, slot, index];
                if (probability > bestProbability)
                {
                    bestProbability = probability;
                    bestIndex = index;
                }
            }

            characters[slot] = Alphabet[bestIndex];
            probabilities[slot] = bestProbability;
        }

        var text = new string(characters).TrimEnd(PadCharacter);
        if (text.Length == 0 || text.Contains(PadCharacter))
        {
            return null;
        }

        var confidence = probabilities[..text.Length].Min();
        return new RecognizedPlate(text, confidence);
    }

    /// <summary>
    /// The resized bitmap as the model's input, `[1, 64, 128, 3]` bytes, each
    /// pixel's red, green and blue in turn. The bitmap is opaque Rgba8888, so
    /// the tensor is filled from the bytes in memory, dropping the alpha byte.
    /// </summary>
    internal static DenseTensor<byte> ToInputTensor(SKBitmap resized)
    {
        if (resized.ColorType != SKColorType.Rgba8888
            || resized.Width != InputWidth
            || resized.Height != InputHeight)
        {
            throw new ArgumentException(
                $"The recogniser input is a {InputWidth}x{InputHeight} Rgba8888 bitmap.", nameof(resized));
        }

        var tensor = new DenseTensor<byte>([1, InputHeight, InputWidth, 3]);
        var pixels = resized.GetPixelSpan();
        var stride = resized.RowBytes;
        var values = tensor.Buffer.Span;
        for (var y = 0; y < InputHeight; y++)
        {
            var source = pixels.Slice(y * stride, InputWidth * 4);
            var target = values.Slice(y * InputWidth * 3, InputWidth * 3);
            for (var x = 0; x < InputWidth; x++)
            {
                target[x * 3] = source[x * 4];
                target[x * 3 + 1] = source[x * 4 + 1];
                target[x * 3 + 2] = source[x * 4 + 2];
            }
        }

        return tensor;
    }
}
