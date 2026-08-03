using Pegasus.Core.ImageIntake;
using Pegasus.Infrastructure.Vision;

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
