using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.ImageIntake;

public sealed record ImageIntakeOrigin(
    Guid ReceiptId,
    IntakeSourceIdentity SourceIdentity,
    string SourceHash,
    Guid EvaluationRevisionId);

/// <summary>
/// A durable pre-Case record for image-only material with a usable normalised
/// VRM. It is never a Case: association with an instructed Case retains both
/// identities, and the Image Intake Reference remains permanent linked history.
/// </summary>
public sealed record ImageIntakeRecord(
    Guid Id,
    ImageIntakeOrigin Origin,
    string NormalizedVehicleRegistration,
    string ImageIntakeReference,
    Guid? LinkedCaseId,
    long Version);

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

public sealed class ImageIntakeVersionConflictException(
    Guid imageIntakeId,
    long expectedVersion,
    long actualVersion)
    : InvalidOperationException(
        $"Image intake '{imageIntakeId}' is at version {actualVersion}, not expected version {expectedVersion}.")
{
    public Guid ImageIntakeId { get; } = imageIntakeId;

    public long ExpectedVersion { get; } = expectedVersion;

    public long ActualVersion { get; } = actualVersion;
}

public sealed class ImageIntakeOperationConflictException(Guid imageIntakeId, string operationKey)
    : InvalidOperationException(
        $"Operation '{operationKey}' was already applied to image intake '{imageIntakeId}' with different inputs.")
{
    public Guid ImageIntakeId { get; } = imageIntakeId;

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

public sealed record ImageIntakeCaseLinkRequest(
    Guid ImageIntakeId,
    Guid CaseId,
    long ExpectedImageIntakeVersion,
    long ExpectedCaseVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string CaseEditLeaseToken);

public interface IRegisterImageIntake
{
    Task<ImageIntakeRecord> ExecuteAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);
}

public interface ILinkImageIntakeCase
{
    Task ExecuteAsync(ImageIntakeCaseLinkRequest request, CancellationToken cancellationToken);
}

public interface IUnlinkImageIntakeCase
{
    Task ExecuteAsync(ImageIntakeCaseLinkRequest request, CancellationToken cancellationToken);
}

public sealed record ImageIntakeHistoryEntry(
    Guid Id,
    Guid ImageIntakeId,
    string EventType,
    string Actor,
    string Reason,
    string OperationKey,
    DateTimeOffset OccurredAtUtc,
    long BeforeVersion,
    long AfterVersion,
    Guid? AfterLinkedCaseId);

public sealed record ImageIntakeSummary(
    Guid Id,
    string ImageIntakeReference,
    string NormalizedVehicleRegistration,
    Guid? LinkedCaseId,
    DateTimeOffset RegisteredAtUtc,
    long Version);

public sealed record ImageIntakeDetail(
    ImageIntakeRecord Record,
    DateTimeOffset RegisteredAtUtc,
    IReadOnlyList<ImageIntakeHistoryEntry> History);

public interface IImageIntakeQueries
{
    Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
        bool? associated,
        CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ImageIntakeDetail?> GetByReferenceAsync(
        string imageIntakeReference,
        CancellationToken cancellationToken);
}

/// <summary>
/// The historical post-operation result for an exact committed request. A replay
/// probe returns <see langword="null"/> only for an unseen operation key; a
/// committed key with a different request fingerprint throws
/// <see cref="ImageIntakeOperationConflictException"/>.
/// </summary>
public sealed record ImageIntakeOperationReplay(ImageIntakeRecord Result);

/// <summary>
/// Persists Image-intake registrations and Case associations. Registration
/// allocates the next per-VRM Image Intake Reference atomically; a reference is
/// never reused, including after unlink. Link and unlink must enforce the
/// supplied versions, the active case edit lease, and
/// <see cref="ImageIntakeLifecycleRules.IsCaseEligibleForAssociation"/> against
/// the current case workflow state inside the same transaction, and append the
/// relationship history entry without deleting any prior entry.
/// </summary>
public interface IImageIntakeStore : IImageIntakeQueries
{
    Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);

    Task<ImageIntakeRecord> RegisterAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken);

    Task LinkCaseAsync(ImageIntakeCaseLinkRequest request, CancellationToken cancellationToken);

    Task UnlinkCaseAsync(ImageIntakeCaseLinkRequest request, CancellationToken cancellationToken);
}
