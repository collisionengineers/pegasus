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
}
