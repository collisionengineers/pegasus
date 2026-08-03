using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

public sealed record ImageIntakeOrigin(
    Guid ReceiptId,
    IntakeSourceIdentity SourceIdentity,
    string SourceHash,
    Guid EvaluationRevisionId);

/// <summary>
/// A durable pre-Case record for image-only material with a usable normalised
/// VRM. It is never a Case, carries no case association of its own: whether it
/// is `Associated with Case` derives from its origin receipt's single current
/// case association, so record and receipt can never disagree. The Image
/// Intake Reference is permanent and never reused.
/// </summary>
public sealed record ImageIntakeRecord(
    Guid Id,
    ImageIntakeOrigin Origin,
    string NormalizedVehicleRegistration,
    string ImageIntakeReference);

/// <summary>
/// Formats the registration-based identity `{normalised VRM}-{sequence}` with a
/// two-digit minimum sequence that expands past `-99` without reuse.
/// </summary>
public static class ImageIntakeReferenceFormat
{
    public static string Create(string normalizedVehicleRegistration, int sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedVehicleRegistration);
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "An Image Intake Reference sequence starts at 1.");
        }

        return $"{normalizedVehicleRegistration}-{sequence:00}";
    }
}

public sealed class ImageIntakeOperationConflictException(Guid originReceiptId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to the image intake for receipt '{originReceiptId}' with different inputs.")
{
    public Guid OriginReceiptId { get; } = originReceiptId;

    public string OperationKey { get; } = operationKey;
}

public sealed class ImageIntakeCaseNotEligibleException(Guid caseId)
    : InvalidOperationException(
        $"Case '{caseId}' is not an eligible pre-report instructed case for Image-intake association.")
{
    public Guid CaseId { get; } = caseId;
}

public sealed record RegisterImageIntakeRequest(
    ImageIntakeOrigin Origin,
    string NormalizedVehicleRegistration,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public interface IRegisterImageIntake
{
    Task<ImageIntakeRecord> ExecuteAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);
}

public sealed record ImageIntakeSummary(
    Guid Id,
    Guid OriginReceiptId,
    string ImageIntakeReference,
    string NormalizedVehicleRegistration,
    Guid? AssociatedCaseId,
    string? AssociatedCaseReference,
    DateTimeOffset RegisteredAtUtc);

public sealed record ImageIntakeDetail(
    ImageIntakeRecord Record,
    DateTimeOffset RegisteredAtUtc,
    Guid? AssociatedCaseId,
    string? AssociatedCaseReference);

public interface IImageIntakeQueries
{
    Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
        bool? associated,
        CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetByReferenceAsync(
        string imageIntakeReference,
        CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
        IReadOnlyCollection<Guid> intakeReceiptIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
        string normalizedVehicleRegistration,
        CancellationToken cancellationToken);
}

/// <summary>
/// The historical result for an exact committed registration. A replay probe
/// returns <see langword="null"/> only for an unseen operation key; a committed
/// key with a different request fingerprint throws
/// <see cref="ImageIntakeOperationConflictException"/>.
/// </summary>
public sealed record ImageIntakeOperationReplay(ImageIntakeRecord Result);

/// <summary>
/// Persists Image-intake registrations. Registration allocates the next
/// per-VRM Image Intake Reference atomically (a reference is never reused),
/// verifies the origin against the persisted receipt and evaluation revision,
/// and moves the receipt's decision to `ImageIntakeRegistered` in the same
/// transaction. An `ImageIntakes` row is immutable after creation; case
/// association lives exclusively on the origin receipt.
/// </summary>
public interface IImageIntakeStore : IImageIntakeQueries
{
    Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);

    Task<ImageIntakeRecord> RegisterAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the registration origin for a processed intake receipt: its source
/// identity, source hash, and latest completed evaluation revision (the
/// web/pipeline-facing receipt record does not expose the revision).
/// </summary>
public interface IImageIntakeOriginResolver
{
    Task<ImageIntakeOrigin?> ResolveOriginAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Eligible pre-report instructed Cases whose confirmed vehicle registration
/// matches a normalised VRM — candidates for a reasoned staff pick and for the
/// automatic unambiguous match. Eligibility (editable pre-report state, no
/// report-sent evidence, not archived) is enforced by the query.
/// </summary>
public interface IImageIntakeCaseCandidates
{
    Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
        string normalizedVehicleRegistration,
        CancellationToken cancellationToken);
}

public sealed record ImageIntakeCaseCandidate(
    Guid CaseId,
    string CaseReference,
    long CaseVersion);
