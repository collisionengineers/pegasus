using Microsoft.ML.OnnxRuntime.Tensors;
using Pegasus.Core.ImageIntake;
using Pegasus.Infrastructure.Vision;
using SkiaSharp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CI-safe engine evidence: loading, hash pinning, abstention and the failure
/// contract on repository fixtures only. The repository holds no genuine
/// plate-bearing image and fabricating one is prohibited, so accuracy
/// evidence lives exclusively in the local corpus evaluation run.
/// </summary>
public sealed class VrmRecognitionEngineTests
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    [Fact]
    public void EmbeddedModelBytesMatchThePinnedManifestHashes()
    {
        var models = VisionModelSet.LoadVerified();

        Assert.Equal("fast-alpr-onnx", models.EngineKey);
        Assert.NotEmpty(models.DetectionModel);
        Assert.NotEmpty(models.RecognitionModel);
        Assert.Contains("plate-detection=", models.ModelHashes);
        Assert.Contains("plate-recognition=", models.ModelHashes);
    }

    [Fact]
    public async Task PlateFreeFixtureImageAbstains()
    {
        using var engine = new OnnxVrmRecognitionEngine();

        var result = await engine.RecognizeAsync(
            Convert.FromBase64String(TinyPngBase64),
            CancellationToken.None);

        Assert.Equal(VrmRecognitionOutcomeKind.NoReadableResult, result.Kind);
        Assert.Empty(result.Candidates);
        Assert.Equal("fast-alpr-onnx", result.EngineKey);
    }

    [Fact]
    public async Task CorruptBytesAreATechnicalFailureNeverASuggestion()
    {
        using var engine = new OnnxVrmRecognitionEngine();

        var result = await engine.RecognizeAsync(
            new byte[] { 0x00, 0x01, 0x02, 0x03 },
            CancellationToken.None);

        Assert.Equal(VrmRecognitionOutcomeKind.TechnicalFailure, result.Kind);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void TamperedModelBytesNeverPassHashVerification()
    {
        var models = VisionModelSet.LoadVerified();
        var pinned = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(models.DetectionModel)).ToLowerInvariant();
        var tampered = (byte[])models.DetectionModel.Clone();
        tampered[0] ^= 0xFF;

        Assert.Same(
            models.DetectionModel,
            VisionModelSet.VerifyModel("plate-detection", models.DetectionModel, pinned));
        Assert.Throws<VisionModelIntegrityException>(
            () => VisionModelSet.VerifyModel("plate-detection", tampered, pinned));
    }

    [Fact]
    public async Task AbsurdDeclaredDimensionsAreRefusedBeforeDecoding()
    {
        // A hand-built PNG whose header declares 100,000 × 100,000 pixels:
        // small on the wire, ~37 GB decoded. The engine must refuse it from
        // the header rather than attempt the allocation.
        using var engine = new OnnxVrmRecognitionEngine();

        var result = await engine.RecognizeAsync(
            PngWithDeclaredDimensions(100_000, 100_000),
            CancellationToken.None);

        Assert.Equal(VrmRecognitionOutcomeKind.TechnicalFailure, result.Kind);
        Assert.Equal("image_dimensions_excessive", result.FailureCode);
        Assert.Empty(result.Candidates);
    }

    // The model inputs are filled straight from the bitmap's bytes. These tests fill a
    // tensor the way the code first did, through SKBitmap.Pixels and the tensor's indexer,
    // and require the same values from the direct fill, for a bitmap whose pixels all
    // differ and whose rows are tight and padded. No real plate is needed: the fill is a
    // pure function of the pixels.
    [Theory]
    [InlineData(0)]
    [InlineData(384 * 4 + 20)]
    public void TheDetectorInputIsTheSameAsTheIndexerFillOfThePixelArray(int rowBytes)
    {
        using var bitmap = SyntheticBitmap(384, 384, rowBytes);

        var tensor = PlateDetector.ToInputTensor(bitmap);

        var expected = new DenseTensor<float>([1, 3, 384, 384]);
        var pixels = bitmap.Pixels;
        for (var y = 0; y < 384; y++)
        {
            for (var x = 0; x < 384; x++)
            {
                var pixel = pixels[y * 384 + x];
                expected[0, 0, y, x] = pixel.Red / 255f;
                expected[0, 1, y, x] = pixel.Green / 255f;
                expected[0, 2, y, x] = pixel.Blue / 255f;
            }
        }

        Assert.Equal(DetectorShape, tensor.Dimensions.ToArray());
        Assert.Equal(expected.Buffer.ToArray(), tensor.Buffer.ToArray());
        // The fixed pixels are set to known values, so the equality above is not two
        // fills that are both wrong in the same way.
        Assert.Equal(1f, tensor[0, 0, 383, 383]);
        Assert.Equal(2 / 255f, tensor[0, 1, 0, 1]);
        Assert.Equal(3 / 255f, tensor[0, 2, 0, 1]);
        Assert.Equal(200 / 255f, tensor[0, 0, 192, 192]);
        Assert.Equal(100 / 255f, tensor[0, 1, 192, 192]);
        Assert.Equal(50 / 255f, tensor[0, 2, 192, 192]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128 * 4 + 12)]
    public void TheRecogniserInputIsTheSameAsTheIndexerFillOfThePixelArray(int rowBytes)
    {
        using var bitmap = SyntheticBitmap(128, 64, rowBytes);

        var tensor = PlateRecognizer.ToInputTensor(bitmap);

        var expected = new DenseTensor<byte>([1, 64, 128, 3]);
        var pixels = bitmap.Pixels;
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 128; x++)
            {
                var pixel = pixels[y * 128 + x];
                expected[0, y, x, 0] = pixel.Red;
                expected[0, y, x, 1] = pixel.Green;
                expected[0, y, x, 2] = pixel.Blue;
            }
        }

        Assert.Equal(RecogniserShape, tensor.Dimensions.ToArray());
        Assert.Equal(expected.Buffer.ToArray(), tensor.Buffer.ToArray());
        Assert.Equal((byte)255, tensor[0, 63, 127, 0]);
        Assert.Equal((byte)2, tensor[0, 0, 1, 1]);
        Assert.Equal((byte)3, tensor[0, 0, 1, 2]);
        Assert.Equal((byte)200, tensor[0, 32, 64, 0]);
        Assert.Equal((byte)100, tensor[0, 32, 64, 1]);
        Assert.Equal((byte)50, tensor[0, 32, 64, 2]);
    }

    [Fact]
    public void ABitmapOfTheWrongShapeIsRefusedNotMisread()
    {
        using var small = SyntheticBitmap(128, 64, 0);
        using var large = SyntheticBitmap(384, 384, 0);

        Assert.Throws<ArgumentException>(() => PlateDetector.ToInputTensor(small));
        Assert.Throws<ArgumentException>(() => PlateRecognizer.ToInputTensor(large));
    }

    [Fact]
    public void ACropIsTheSourceSubsetAndAnImpossibleOneIsNull()
    {
        using var source = SyntheticBitmap(200, 100, 0);

        using var crop = OnnxVrmRecognitionEngine.Crop(source, new DetectedPlate(10.4f, 20.2f, 60.1f, 45.5f, 0.9f));

        Assert.NotNull(crop);
        Assert.Equal(51, crop.Width);
        Assert.Equal(26, crop.Height);
        Assert.Equal(source.GetPixel(10, 20), crop.GetPixel(0, 0));
        Assert.Equal(source.GetPixel(60, 45), crop.GetPixel(50, 25));
        Assert.Null(OnnxVrmRecognitionEngine.Crop(source, new DetectedPlate(10, 10, 11, 40, 0.9f)));
        Assert.Null(OnnxVrmRecognitionEngine.Crop(source, new DetectedPlate(300, 10, 400, 40, 0.9f)));
    }

    /// <summary>
    /// An opaque Rgba8888 bitmap whose colour changes with position, so a shifted,
    /// mirrored or wrongly ordered fill differs from the right one everywhere. A few
    /// pixels carry fixed values. A non-zero <paramref name="rowBytes"/> pads each row.
    /// </summary>
    private static SKBitmap SyntheticBitmap(int width, int height, int rowBytes)
    {
        var bitmap = new SKBitmap();
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        Assert.True(rowBytes == 0 ? bitmap.TryAllocPixels(info) : bitmap.TryAllocPixels(info, rowBytes));
        var pixels = bitmap.GetPixelSpan();
        pixels.Fill(0x5A);
        var stride = bitmap.RowBytes;

        static void Set(Span<byte> pixels, int stride, int x, int y, byte red, byte green, byte blue)
        {
            var at = y * stride + x * 4;
            pixels[at] = red;
            pixels[at + 1] = green;
            pixels[at + 2] = blue;
            pixels[at + 3] = 255;
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                Set(
                    pixels,
                    stride,
                    x,
                    y,
                    (byte)(x * 255 / (width - 1)),
                    (byte)(y * 255 / (height - 1)),
                    (byte)((x * 7 + y * 13) % 256));
            }
        }

        Set(pixels, stride, 0, 0, 0, 0, 0);
        Set(pixels, stride, 1, 0, 1, 2, 3);
        Set(pixels, stride, width / 2, height / 2, 200, 100, 50);
        Set(pixels, stride, width - 1, height - 1, 255, 255, 255);
        return bitmap;
    }

    private static readonly int[] DetectorShape = [1, 3, 384, 384];
    private static readonly int[] RecogniserShape = [1, 64, 128, 3];

    private static byte[] PngWithDeclaredDimensions(int width, int height)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(stream, "IHDR", header);
        WriteChunk(stream, "IDAT", [0x78, 0x9C, 0x03, 0x00, 0x00, 0x00, 0x00, 0x01]);
        WriteChunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void WriteChunk(MemoryStream stream, string type, byte[] payload)
    {
        var lengthBytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(lengthBytes, payload.Length);
        stream.Write(lengthBytes);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(payload);
        var crcBytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            crcBytes,
            Crc32(typeBytes, payload));
        stream.Write(crcBytes);
    }

    private static uint Crc32(byte[] type, byte[] payload)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in type.Concat(payload))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
