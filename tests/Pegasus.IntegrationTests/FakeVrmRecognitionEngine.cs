using Pegasus.Core.ImageIntake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A deterministic engine substitute for pipeline tests: the repository holds
/// no genuine plate-bearing image and fabricating one is prohibited, so
/// automatic-flow evidence uses this port fake while the real ONNX engine is
/// covered by its own loading/abstention tests.
/// </summary>
internal sealed class FakeVrmRecognitionEngine(
    string? registration = null) : IVrmRecognitionEngine
{
    public Task<VrmRecognitionResult> RecognizeAsync(
        ReadOnlyMemory<byte> imageBytes,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(registration is null
            ? new VrmRecognitionResult(
                VrmRecognitionOutcomeKind.NoReadableResult,
                [],
                "fake-engine",
                "1",
                "plate-detection=fake;plate-recognition=fake")
            : new VrmRecognitionResult(
                VrmRecognitionOutcomeKind.Suggested,
                [new(registration, registration, 0.95, null)],
                "fake-engine",
                "1",
                "plate-detection=fake;plate-recognition=fake"));
    }
}
