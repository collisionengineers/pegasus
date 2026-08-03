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

        var tensor = new DenseTensor<byte>([1, InputHeight, InputWidth, 3]);
        var pixels = resized.Pixels;
        for (var y = 0; y < InputHeight; y++)
        {
            var row = y * InputWidth;
            for (var x = 0; x < InputWidth; x++)
            {
                var pixel = pixels[row + x];
                tensor[0, y, x, 0] = pixel.Red;
                tensor[0, y, x, 1] = pixel.Green;
                tensor[0, y, x, 2] = pixel.Blue;
            }
        }

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
}
