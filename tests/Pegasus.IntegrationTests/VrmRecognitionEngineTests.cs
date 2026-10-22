using Microsoft.ML.OnnxRuntime;
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

    // The detector is shown a smaller decode of a JPEG and the crops come from the native
    // pixels. No genuine plate is in the repository, so these show what can be shown without
    // one: the decode sizes, that the detector's input and boxes do not move with the smaller
    // decode, and, over fixed detections and readings, that the plate the recogniser offers
    // does not change when its inferences are capped.
    [Fact]
    public void AJpegIsDecodedSmallerForDetectionButNeverBelowTheInputSize()
    {
        using var data = SKData.CreateCopy(SyntheticImage(2000, 1500, SKEncodedImageFormat.Jpeg));
        using var codec = SKCodec.Create(data);

        var size = OnnxVrmRecognitionEngine.DetectionDecodeSize(codec);

        Assert.True(size.Width < 2000 && size.Height < 1500, $"decoded {size.Width}x{size.Height}");
        Assert.True(Math.Max(size.Width, size.Height) >= PlateDetector.InputSize, $"decoded {size.Width}x{size.Height}");
        Assert.True(Math.Max(size.Width, size.Height) <= 1000, $"decoded {size.Width}x{size.Height}");
    }

    [Fact]
    public void AnImageThatCannotBeDecodedSmallerOrIsAlreadySmallIsDecodedWhole()
    {
        using var png = SKData.CreateCopy(SyntheticImage(2000, 1500, SKEncodedImageFormat.Png));
        using var pngCodec = SKCodec.Create(png);
        using var small = SKData.CreateCopy(SyntheticImage(300, 200, SKEncodedImageFormat.Jpeg));
        using var smallCodec = SKCodec.Create(small);

        Assert.Equal(new SKSizeI(2000, 1500), OnnxVrmRecognitionEngine.DetectionDecodeSize(pngCodec));
        Assert.Equal(new SKSizeI(300, 200), OnnxVrmRecognitionEngine.DetectionDecodeSize(smallCodec));
    }

    [Fact]
    public void TheDetectorSeesTheSameInputAndFindsTheSameBoxesFromTheSmallerDecode()
    {
        using var data = SKData.CreateCopy(SyntheticImage(2000, 1500, SKEncodedImageFormat.Jpeg));
        using var codec = SKCodec.Create(data);
        var size = OnnxVrmRecognitionEngine.DetectionDecodeSize(codec);
        using var native = SKBitmap.Decode(codec);
        using var smaller = SKBitmap.Decode(
            codec,
            new SKImageInfo(size.Width, size.Height, codec.Info.ColorType, codec.Info.AlphaType));
        Assert.Equal(new SKSizeI(2000, 1500), native.Info.Size);
        Assert.Equal(size, smaller.Info.Size);

        // 2000 x 1500 letterboxes to 384 x 288 with 48 rows of padding above and below.
        using var fromNative = PlateDetector.Letterbox(native, 384, 288, 0, 48);
        using var fromSmaller = PlateDetector.Letterbox(smaller, 384, 288, 0, 48);
        var nativeInput = PlateDetector.ToInputTensor(fromNative!).Buffer.ToArray();
        var smallerInput = PlateDetector.ToInputTensor(fromSmaller!).Buffer.ToArray();
        var meanDifference = nativeInput.Zip(smallerInput, (a, b) => Math.Abs(a - b)).Average();
        Assert.True(meanDifference < 0.02, $"mean input difference {meanDifference}");

        using var session = new InferenceSession(VisionModelSet.LoadVerified().DetectionModel);
        var detector = new PlateDetector(session);
        var nativeBoxes = detector.Detect(native, 2000, 1500, 0.35);
        var smallerBoxes = detector.Detect(smaller, 2000, 1500, 0.35);

        Assert.Equal(nativeBoxes.Count, smallerBoxes.Count);
        for (var index = 0; index < nativeBoxes.Count; index++)
        {
            Assert.InRange(smallerBoxes[index].Left, nativeBoxes[index].Left - 8, nativeBoxes[index].Left + 8);
            Assert.InRange(smallerBoxes[index].Top, nativeBoxes[index].Top - 8, nativeBoxes[index].Top + 8);
            Assert.InRange(smallerBoxes[index].Right, nativeBoxes[index].Right - 8, nativeBoxes[index].Right + 8);
            Assert.InRange(smallerBoxes[index].Bottom, nativeBoxes[index].Bottom - 8, nativeBoxes[index].Bottom + 8);
            Assert.InRange(smallerBoxes[index].Score, nativeBoxes[index].Score - 0.05f, nativeBoxes[index].Score + 0.05f);
        }
    }

    [Fact]
    public async Task APlateFreeJpegAbstainsFromTheSmallerDecodeAlone()
    {
        using var engine = new OnnxVrmRecognitionEngine();

        var result = await engine.RecognizeAsync(
            FlatImage(1600, 1200),
            CancellationToken.None);

        Assert.Equal(VrmRecognitionOutcomeKind.NoReadableResult, result.Kind);
        Assert.Equal("detector_no_plate", result.FailureCode);
        Assert.Empty(result.Candidates);
    }

    // Detections arrive best score first. Each fixture is a run of (score, text, confidence)
    // readings; the capped selection must offer the same plate as the uncapped one it replaced.
    public static TheoryData<string, (float Score, string? Text, double Confidence)[]> SelectionFixtures => new()
    {
        { "single", [(0.82f, "AB12 CDE", 0.93)] },
        { "three-same-plate", [(0.91f, "AB12 CDE", 0.95), (0.74f, "AB12 CDE", 0.88), (0.52f, "AB12 CDE", 0.7)] },
        { "five-same-plate", [(0.91f, "AB12CDE", 0.95), (0.74f, "AB12CDE", 0.88), (0.52f, "AB12CDE", 0.7), (0.41f, "AB12CDE", 0.6), (0.36f, "AB12CDE", 0.5)] },
        { "best-is-third", [(0.9f, "XY99ZZZ", 0.4), (0.8f, "AB12CDE", 0.3), (0.7f, "LM34NOP", 0.69), (0.6f, "QR56STU", 0.2), (0.5f, "VW78XYZ", 0.1)] },
        { "late-ones-unreadable", [(0.9f, "AB12CDE", 0.9), (0.8f, null, 0), (0.7f, "A", 0.99), (0.6f, null, 0), (0.5f, "A", 0.9)] },
        { "first-unreadable", [(0.9f, null, 0), (0.8f, "AB12CDE", 0.85), (0.7f, "LM34NOP", 0.6), (0.6f, "QR56STU", 0.2)] },
    };

    [Theory]
    [MemberData(nameof(SelectionFixtures))]
    public void CappingTheRecogniserOffersTheSamePlateAsReadingEveryDetection(
        string fixture,
        (float Score, string? Text, double Confidence)[] readings)
    {
        Assert.NotEmpty(fixture);
        var plates = readings.Select(reading => new DetectedPlate(10, 10, 60, 40, reading.Score)).ToArray();
        var recognizerCalls = 0;
        var capped = OnnxVrmRecognitionEngine.RecognizeCandidates(
            plates,
            plate => CropNamedByScore(plate),
            crop =>
            {
                recognizerCalls++;
                return Reading(readings, crop);
            });
        var uncapped = UncappedCandidates(plates, readings);

        Assert.True(recognizerCalls <= OnnxVrmRecognitionEngine.MaximumRecognizerInferences);
        Assert.Equal(
            uncapped.OrderByDescending(candidate => candidate.Confidence).FirstOrDefault(),
            capped.OrderByDescending(candidate => candidate.Confidence).FirstOrDefault());
    }

    [Fact]
    public void TheRecogniserRunsOnTheBestThreeDetectionsOnly()
    {
        var readings = new (float Score, string? Text, double Confidence)[]
        {
            (0.9f, "AB12CDE", 0.9), (0.8f, "AB12CDE", 0.8), (0.7f, "AB12CDE", 0.7),
            (0.6f, "LM34NOP", 0.6), (0.5f, "QR56STU", 0.5),
        };
        var plates = readings.Select(reading => new DetectedPlate(10, 10, 60, 40, reading.Score)).ToArray();
        var cropped = new List<float>();
        var recognized = new List<int>();

        var candidates = OnnxVrmRecognitionEngine.RecognizeCandidates(
            plates,
            plate =>
            {
                cropped.Add(plate.Score);
                return CropNamedByScore(plate);
            },
            crop =>
            {
                recognized.Add(crop.Width);
                return Reading(readings, crop);
            });

        Assert.Equal([0.9f, 0.8f, 0.7f], cropped);
        Assert.Equal([90, 80, 70], recognized);
        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, candidate => Assert.Equal("AB12CDE", candidate.NormalizedRegistration));
    }

    [Fact]
    public void ADetectionTooSmallToCropCostsNoInferenceAndKeepsItsTurnForTheNext()
    {
        var readings = new (float Score, string? Text, double Confidence)[]
        {
            (0.9f, "NOPE", 0.9), (0.8f, "AB12CDE", 0.8), (0.7f, "LM34NOP", 0.7),
            (0.6f, "QR56STU", 0.6), (0.5f, "VW78XYZ", 0.5),
        };
        var plates = readings.Select(reading => new DetectedPlate(10, 10, 60, 40, reading.Score)).ToArray();
        var recognized = new List<int>();

        var candidates = OnnxVrmRecognitionEngine.RecognizeCandidates(
            plates,
            plate => plate.Score > 0.85f ? null : CropNamedByScore(plate),
            crop =>
            {
                recognized.Add(crop.Width);
                return Reading(readings, crop);
            });

        Assert.Equal([80, 70, 60], recognized);
        Assert.Equal(["AB12CDE", "LM34NOP", "QR56STU"], candidates.Select(candidate => candidate.NormalizedRegistration));
    }

    [Fact]
    public void AFourthDetectionNeverOffersItsPlate()
    {
        // The one place the cap is allowed to differ, pinned so a change to it is seen: three
        // weakly read detections and a fourth read with more confidence than any of them.
        var readings = new (float Score, string? Text, double Confidence)[]
        {
            (0.9f, "AB12CDE", 0.4), (0.8f, "AB12CDE", 0.4), (0.7f, "AB12CDE", 0.4), (0.6f, "LM34NOP", 0.99),
        };
        var plates = readings.Select(reading => new DetectedPlate(10, 10, 60, 40, reading.Score)).ToArray();

        var capped = OnnxVrmRecognitionEngine.RecognizeCandidates(
            plates, plate => CropNamedByScore(plate), crop => Reading(readings, crop));
        var uncapped = UncappedCandidates(plates, readings);

        Assert.DoesNotContain(capped, candidate => candidate.NormalizedRegistration == "LM34NOP");
        Assert.Equal("LM34NOP", uncapped.OrderByDescending(candidate => candidate.Confidence).First().NormalizedRegistration);
    }

    // A crop whose width is the detection's score in hundredths, so the fake recogniser can
    // tell which detection it was handed.
    private static SKBitmap CropNamedByScore(DetectedPlate plate) =>
        new(new SKImageInfo((int)Math.Round(plate.Score * 100), 4));

    private static RecognizedPlate? Reading((float Score, string? Text, double Confidence)[] readings, SKBitmap crop)
    {
        var reading = readings.Single(candidate => (int)Math.Round(candidate.Score * 100) == crop.Width);
        return reading.Text is null ? null : new RecognizedPlate(reading.Text, reading.Confidence);
    }

    // The selection as it was before the cap: every detection is read.
    private static List<VrmPlateCandidate> UncappedCandidates(
        DetectedPlate[] plates,
        (float Score, string? Text, double Confidence)[] readings)
    {
        var candidates = new List<VrmPlateCandidate>();
        foreach (var plate in plates)
        {
            using var crop = CropNamedByScore(plate);
            if (Reading(readings, crop) is not { } recognized)
            {
                continue;
            }

            var normalized = new string(recognized.Text
                .ToUpperInvariant()
                .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
                .ToArray());
            if (normalized.Length is < 2 or > 20)
            {
                continue;
            }

            candidates.Add(new VrmPlateCandidate(
                recognized.Text,
                normalized,
                Math.Min(plate.Score, recognized.Confidence),
                new VrmPlateBounds(plate.Left, plate.Top, plate.Right, plate.Bottom)));
        }

        return candidates;
    }

    // Smooth ground with two hard-edged blocks: a decode at a smaller scale and a resize of the
    // native pixels agree closely on it, as they do on most of a photograph.
    private static byte[] SyntheticImage(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            using var ground = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(width, height),
                    [new SKColor(60, 80, 110), new SKColor(150, 160, 170)],
                    SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(0, 0, width, height, ground);
            using var block = new SKPaint { Color = new SKColor(200, 190, 60) };
            canvas.DrawRect(width * 0.3f, height * 0.6f, width * 0.25f, height * 0.08f, block);
            block.Color = new SKColor(30, 30, 30);
            canvas.DrawRect(width * 0.1f, height * 0.1f, width * 0.2f, height * 0.3f, block);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }

    private static byte[] FlatImage(int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Erase(new SKColor(120, 120, 120));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
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
