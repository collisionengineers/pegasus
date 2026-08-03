using Pegasus.Core.Identity;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// The closed recognition outcome taxonomy the operator surface must
/// distinguish: a suggestion, no readable result, a per-image technical
/// failure, and an unusable engine dependency. An empty value is never
/// rendered as success.
/// </summary>
public enum VrmRecognitionOutcomeKind
{
    Suggested,
    NoReadableResult,
    TechnicalFailure,
    Unavailable
}

public sealed record VrmPlateBounds(
    double Left,
    double Top,
    double Right,
    double Bottom);

public sealed record VrmPlateCandidate(
    string PlateText,
    string NormalizedRegistration,
    double Confidence,
    VrmPlateBounds? Bounds);

public sealed record VrmRecognitionResult(
    VrmRecognitionOutcomeKind Kind,
    IReadOnlyList<VrmPlateCandidate> Candidates,
    string EngineKey,
    string EngineVersion,
    string ModelHashes,
    string? FailureCode = null,
    string? FailureReason = null);

/// <summary>
/// The in-process ADR-0019 recognition port: image bytes in, a recognition
/// result out. The engine never mutates anything, never uploads an image
/// anywhere, and fails toward abstention rather than a guessed registration.
/// </summary>
public interface IVrmRecognitionEngine
{
    Task<VrmRecognitionResult> RecognizeAsync(
        ReadOnlyMemory<byte> imageBytes,
        CancellationToken cancellationToken);
}

/// <summary>
/// The operator-accepted automatic-action bar (2026-08-03, closing open
/// decision 1 from the full-cohort evaluation run 20260803-092906). Below
/// the bar the pipeline records suggestions only and staff paths take over.
/// </summary>
public static class VrmRecognitionProvisionalBar
{
    /// <summary>
    /// Minimum supplied candidate confidence for a read the pipeline may act
    /// on automatically. Operator-accepted at 0.80 (2026-08-03): with the
    /// accepted match rules the full cohort measured a 3.2% bounded
    /// genuine-misread rate among suggestions at five times the coverage of
    /// 0.90; third-party reads in multi-vehicle photos account for the rest
    /// and ambiguity always abstains.
    /// </summary>
    public const double MinimumAutomaticConfidence = 0.80;

    /// <summary>
    /// Automatic registration additionally requires exactly one distinct
    /// normalised VRM across every confident read for the receipt.
    /// </summary>
    public const int RequiredDistinctRegistrations = 1;
}

/// <summary>
/// The operator-directed match rule (2026-08-03): a read matches a confirmed
/// registration exactly; with exactly one character missing (a truncated read
/// such as `BX69YL` for `BX69YLM` — the confirmed value supplies the missing
/// character); or when a read one character longer than the standard
/// seven-character registration carries a `1` in the fifth position and
/// matches once that character is dropped (`PK201YHR` for `PK20YHR` — plate
/// furniture such as a screw or divider is commonly read as an inserted `1`).
/// A substituted character is never a match: only the confirmed registration
/// can complete or de-noise a read, not correct one.
/// </summary>
public static class VrmRegistrationMatching
{
    private const int StandardRegistrationLength = 7;
    private const int InsertedMarkIndex = 4;
    private const char InsertedMarkCharacter = '1';

    public static bool IsMatch(string read, string confirmedRegistration)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(confirmedRegistration);
        return string.Equals(read, confirmedRegistration, StringComparison.Ordinal)
            || IsOneCharacterMissing(read, confirmedRegistration)
            || IsFifthCharacterInsertionMatch(read, confirmedRegistration);
    }

    /// <summary>
    /// A read one character longer than a standard registration whose fifth
    /// character is a `1` is retried without that character; if the result
    /// matches (exactly or with one character missing), the confirmed
    /// registration is assumed correct.
    /// </summary>
    public static bool IsFifthCharacterInsertionMatch(string read, string confirmedRegistration)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(confirmedRegistration);
        if (read.Length != StandardRegistrationLength + 1
            || read[InsertedMarkIndex] != InsertedMarkCharacter)
        {
            return false;
        }

        var withoutInsertedMark = read.Remove(InsertedMarkIndex, 1);
        return string.Equals(withoutInsertedMark, confirmedRegistration, StringComparison.Ordinal)
            || IsOneCharacterMissing(withoutInsertedMark, confirmedRegistration);
    }

    public static bool IsOneCharacterMissing(string read, string confirmedRegistration)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(confirmedRegistration);
        if (read.Length != confirmedRegistration.Length - 1 || read.Length == 0)
        {
            return false;
        }

        var readIndex = 0;
        var skipped = false;
        for (var confirmedIndex = 0; confirmedIndex < confirmedRegistration.Length; confirmedIndex++)
        {
            if (readIndex < read.Length
                && read[readIndex] == confirmedRegistration[confirmedIndex])
            {
                readIndex++;
                continue;
            }

            if (skipped)
            {
                return false;
            }

            skipped = true;
        }

        return readIndex == read.Length;
    }
}

public enum ImageVrmSuggestionDisposition
{
    Pending,
    Confirmed,
    Dismissed
}

public sealed record ImageVrmSuggestion(
    Guid Id,
    Guid IntakeReceiptId,
    Guid IntakeAssetId,
    string StorageKey,
    string ContentHash,
    string EngineKey,
    string EngineVersion,
    string ModelHashes,
    VrmRecognitionOutcomeKind Outcome,
    string? SuggestedRegistration,
    double? Confidence,
    string? FailureCode,
    string? FailureReason,
    DateTimeOffset OccurredAtUtc,
    ImageVrmSuggestionDisposition Disposition,
    string? DispositionActor,
    string? DispositionReason,
    DateTimeOffset? DisposedAtUtc);

public sealed record ImageVrmSuggestionDraft(
    Guid IntakeReceiptId,
    Guid IntakeAssetId,
    string StorageKey,
    string ContentHash,
    string EngineKey,
    string EngineVersion,
    string ModelHashes,
    VrmRecognitionOutcomeKind Outcome,
    string? SuggestedRegistration,
    double? Confidence,
    string? FailureCode,
    string? FailureReason,
    string OperationKey);

public sealed record ImageVrmSuggestionDispositionRequest(
    Guid SuggestionId,
    ImageVrmSuggestionDisposition Disposition,
    ActionActor Actor,
    string Reason,
    string OperationKey);

/// <summary>
/// Persists every recognition run's outcome bound to its retained source
/// image, separately from confirmed case data. Recording is idempotent by
/// operation key; abstention and failure are first-class recorded outcomes.
/// </summary>
public interface IVrmSuggestionStore
{
    Task<ImageVrmSuggestion> RecordAsync(
        ImageVrmSuggestionDraft draft,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageVrmSuggestion>> ListForReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);

    Task<ImageVrmSuggestion?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ImageVrmSuggestion> SetDispositionAsync(
        ImageVrmSuggestionDispositionRequest request,
        CancellationToken cancellationToken);
}
