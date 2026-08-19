using Microsoft.ML.OnnxRuntime;
using Pegasus.Core.ImageIntake;
using SkiaSharp;

namespace Pegasus.Infrastructure.Vision;

/// <summary>
/// The in-process ADR-0019 engine: vendored hash-verified ONNX plate
/// detection and recognition, bytes in and a result out. It performs no I/O
/// beyond the supplied bytes, never uploads an image anywhere, and fails
/// toward abstention: an unusable model set is `Unavailable`, an undecodable
/// or failing image is `TechnicalFailure`, and anything unreadable is
/// `NoReadableResult` rather than a guess.
/// </summary>
public sealed class OnnxVrmRecognitionEngine : IVrmRecognitionEngine, IDisposable
{
    /// <summary>
    /// Provisional detection-score floor for even reporting a candidate;
    /// candidates the automation may act on are additionally gated by
    /// <see cref="VrmRecognitionProvisionalBar"/>.
    /// </summary>
    private const double DetectionScoreThreshold = 0.35;

    /// <summary>
    /// Upper bound on decoded pixels (width × height). Intake caps the
    /// compressed bytes, but a small compressed image can still declare
    /// enormous dimensions and demand gigabytes on decode —
    /// <see cref="OutOfMemoryException"/> is deliberately non-recoverable in
    /// the intake pipeline, so absurd dimensions are refused from the header
    /// as a technical failure before any pixel is allocated. Fifty megapixels
    /// (~200 MB decoded RGBA) clears every ordinary phone photograph.
    /// </summary>
    private const long MaximumDecodedPixels = 50_000_000;

    private readonly Lazy<EngineState> _state;

    public OnnxVrmRecognitionEngine()
    {
        _state = new Lazy<EngineState>(
            EngineState.Create,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Task<VrmRecognitionResult> RecognizeAsync(
        ReadOnlyMemory<byte> imageBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Recognize(imageBytes));
    }

    private VrmRecognitionResult Recognize(ReadOnlyMemory<byte> imageBytes)
    {
        EngineState state;
        try
        {
            state = _state.Value;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return new(
                VrmRecognitionOutcomeKind.Unavailable,
                [],
                "fast-alpr-onnx",
                "1",
                string.Empty,
                "engine_unavailable",
                "The recognition engine dependency could not be initialised.");
        }

        try
        {
            using var imageData = SKData.CreateCopy(imageBytes.Span);
            using var codec = SKCodec.Create(imageData);
            if (codec is null)
            {
                return Failure(
                    state,
                    VrmRecognitionOutcomeKind.TechnicalFailure,
                    "image_decode_failure",
                    "The image bytes could not be decoded.");
            }

            if ((long)codec.Info.Width * codec.Info.Height is <= 0 or > MaximumDecodedPixels)
            {
                return Failure(
                    state,
                    VrmRecognitionOutcomeKind.TechnicalFailure,
                    "image_dimensions_excessive",
                    $"The image declares {codec.Info.Width}x{codec.Info.Height} pixels, " +
                    $"outside the {MaximumDecodedPixels:N0}-pixel decode bound.");
            }

            using var bitmap = SKBitmap.Decode(codec);
            if (bitmap is null)
            {
                return Failure(
                    state,
                    VrmRecognitionOutcomeKind.TechnicalFailure,
                    "image_decode_failure",
                    "The image bytes could not be decoded.");
            }

            var candidates = new List<VrmPlateCandidate>();
            var detectedPlates = state.Detector.Detect(bitmap, DetectionScoreThreshold).ToArray();
            foreach (var plate in detectedPlates)
            {
                var crop = Crop(bitmap, plate);
                if (crop is null)
                {
                    continue;
                }

                RecognizedPlate? recognized;
                using (crop)
                {
                    recognized = state.Recognizer.Recognize(crop);
                }

                if (recognized is null)
                {
                    continue;
                }

                var normalized = Normalize(recognized.Text);
                if (normalized is null)
                {
                    continue;
                }

                candidates.Add(new VrmPlateCandidate(
                    recognized.Text,
                    normalized,
                    Math.Min(plate.Score, recognized.Confidence),
                    new VrmPlateBounds(plate.Left, plate.Top, plate.Right, plate.Bottom)));
            }

            return candidates.Count == 0
                ? Failure(
                    state,
                    VrmRecognitionOutcomeKind.NoReadableResult,
                    detectedPlates.Length == 0 ? "detector_no_plate" : "recognizer_no_readable_text",
                    detectedPlates.Length == 0
                        ? "The vision detector found no registration plate in the image."
                        : "The vision detector found a plate, but the recognizer produced no readable registration text.")
                : new(
                    VrmRecognitionOutcomeKind.Suggested,
                    candidates.OrderByDescending(candidate => candidate.Confidence).ToArray(),
                    state.EngineKey,
                    state.EngineVersion,
                    state.ModelHashes);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Failure(
                state,
                VrmRecognitionOutcomeKind.TechnicalFailure,
                "recognition_failure",
                "Plate detection or recognition failed for this image.");
        }
    }

    private static VrmRecognitionResult Failure(
        EngineState state,
        VrmRecognitionOutcomeKind kind,
        string? failureCode,
        string? failureReason) => new(
        kind,
        [],
        state.EngineKey,
        state.EngineVersion,
        state.ModelHashes,
        failureCode,
        failureReason);

    private static SKBitmap? Crop(SKBitmap source, DetectedPlate plate)
    {
        var left = (int)Math.Floor(plate.Left);
        var top = (int)Math.Floor(plate.Top);
        var right = (int)Math.Ceiling(plate.Right);
        var bottom = (int)Math.Ceiling(plate.Bottom);
        var bounds = SKRectI.Intersect(
            new SKRectI(left, top, right, bottom),
            new SKRectI(0, 0, source.Width, source.Height));
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return null;
        }

        var crop = new SKBitmap(
            new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        return source.ExtractSubset(crop, bounds) ? crop : null;
    }

    private static string? Normalize(string plateText)
    {
        var normalized = new string(plateText
            .ToUpperInvariant()
            .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
            .ToArray());
        return normalized.Length is >= 2 and <= 20 ? normalized : null;
    }

    public void Dispose()
    {
        if (_state.IsValueCreated)
        {
            _state.Value.Dispose();
        }
    }

    private sealed class EngineState : IDisposable
    {
        private EngineState(
            string engineKey,
            string engineVersion,
            string modelHashes,
            InferenceSession detectionSession,
            InferenceSession recognitionSession)
        {
            EngineKey = engineKey;
            EngineVersion = engineVersion;
            ModelHashes = modelHashes;
            DetectionSession = detectionSession;
            RecognitionSession = recognitionSession;
            Detector = new PlateDetector(detectionSession);
            Recognizer = new PlateRecognizer(recognitionSession);
        }

        public string EngineKey { get; }

        public string EngineVersion { get; }

        public string ModelHashes { get; }

        public PlateDetector Detector { get; }

        public PlateRecognizer Recognizer { get; }

        private InferenceSession DetectionSession { get; }

        private InferenceSession RecognitionSession { get; }

        public static EngineState Create()
        {
            var models = VisionModelSet.LoadVerified();
            var detectionSession = new InferenceSession(models.DetectionModel);
            var recognitionSession = new InferenceSession(models.RecognitionModel);
            return new EngineState(
                models.EngineKey,
                models.EngineVersion,
                models.ModelHashes,
                detectionSession,
                recognitionSession);
        }

        public void Dispose()
        {
            DetectionSession.Dispose();
            RecognitionSession.Dispose();
        }
    }
}
