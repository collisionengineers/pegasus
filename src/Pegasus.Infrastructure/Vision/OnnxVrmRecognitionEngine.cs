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

    /// <summary>
    /// The most plates one image sends to the recogniser (the Worker's bound of three, R45),
    /// best detection score first. The detector can return many boxes above the score floor,
    /// and a recogniser inference costs the same for each.
    /// </summary>
    internal const int MaximumRecognizerInferences = 3;

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

            // The detector reads a 384-pixel input, so it is shown the smallest decode the codec
            // offers that still has that many pixels on the longest side; the crops are cut from
            // the native pixels, decoded only once a plate has been found.
            SKBitmap? native = null;
            try
            {
                var detectionSize = DetectionDecodeSize(codec);
                var scaledDetection = detectionSize != codec.Info.Size;
                using var scaled = scaledDetection
                    ? SKBitmap.Decode(
                        codec,
                        new SKImageInfo(
                            detectionSize.Width,
                            detectionSize.Height,
                            codec.Info.ColorType,
                            codec.Info.AlphaType))
                    : null;
                if (!scaledDetection)
                {
                    native = SKBitmap.Decode(codec);
                }

                var detectionImage = scaled ?? native;
                if (detectionImage is null)
                {
                    return DecodeFailure(state);
                }

                var detectedPlates = state.Detector
                    .Detect(detectionImage, codec.Info.Width, codec.Info.Height, DetectionScoreThreshold)
                    .ToArray();
                if (detectedPlates.Length == 0)
                {
                    return Failure(
                        state,
                        VrmRecognitionOutcomeKind.NoReadableResult,
                        "detector_no_plate",
                        "The vision detector found no registration plate in the image.");
                }

                var cropSource = native ?? SKBitmap.Decode(codec);
                if (cropSource is null)
                {
                    return DecodeFailure(state);
                }

                native = cropSource;
                var candidates = RecognizeCandidates(
                    detectedPlates,
                    plate => Crop(cropSource, plate),
                    state.Recognizer.Recognize);

                return candidates.Count == 0
                    ? Failure(
                        state,
                        VrmRecognitionOutcomeKind.NoReadableResult,
                        "recognizer_no_readable_text",
                        "The vision detector found a plate, but the recognizer produced no readable registration text.")
                    : new(
                        VrmRecognitionOutcomeKind.Suggested,
                        candidates.OrderByDescending(candidate => candidate.Confidence).ToArray(),
                        state.EngineKey,
                        state.EngineVersion,
                        state.ModelHashes);
            }
            finally
            {
                native?.Dispose();
            }
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

    private static VrmRecognitionResult DecodeFailure(EngineState state) => Failure(
        state,
        VrmRecognitionOutcomeKind.TechnicalFailure,
        "image_decode_failure",
        "The image bytes could not be decoded.");

    /// <summary>
    /// The size the codec decodes to for detection: the smallest it offers whose longest side
    /// still holds the detector's input; for a JPEG a whole number of eighths of the native size,
    /// for any other format the native size itself.
    /// </summary>
    internal static SKSizeI DetectionDecodeSize(SKCodec codec)
    {
        var info = codec.Info;
        var scale = Math.Min(1f, (float)PlateDetector.InputSize / Math.Max(info.Width, info.Height));
        var size = codec.GetScaledDimensions(scale);
        while (Math.Max(size.Width, size.Height) < PlateDetector.InputSize && scale < 1f)
        {
            scale = Math.Min(1f, scale + 0.125f);
            size = codec.GetScaledDimensions(scale);
        }

        return size;
    }

    /// <summary>
    /// The candidates for the detected plates, which arrive best score first. The recogniser runs
    /// on at most <see cref="MaximumRecognizerInferences"/> crops; a detection too small to crop
    /// costs no inference and does not use one of them.
    /// </summary>
    internal static List<VrmPlateCandidate> RecognizeCandidates(
        IEnumerable<DetectedPlate> plates,
        Func<DetectedPlate, SKBitmap?> cropOf,
        Func<SKBitmap, RecognizedPlate?> recognize)
    {
        var candidates = new List<VrmPlateCandidate>();
        var inferences = 0;
        foreach (var plate in plates)
        {
            if (inferences == MaximumRecognizerInferences)
            {
                break;
            }

            var crop = cropOf(plate);
            if (crop is null)
            {
                continue;
            }

            RecognizedPlate? recognized;
            using (crop)
            {
                inferences++;
                recognized = recognize(crop);
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

        return candidates;
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

    internal static SKBitmap? Crop(SKBitmap source, DetectedPlate plate)
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

        // ExtractSubset makes the crop a view of the source's pixels and takes the source's
        // format, so the crop starts empty: allocating pixels here would be replaced at once.
        var crop = new SKBitmap();
        if (source.ExtractSubset(crop, bounds))
        {
            return crop;
        }

        crop.Dispose();
        return null;
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
