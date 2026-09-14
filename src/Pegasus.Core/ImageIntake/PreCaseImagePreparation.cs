using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// Crop and tag on a pre-Case image (v26, 13 September): an image on an
/// image-initiated Case, a Triage or an Unidentified item carries the same
/// stored crop rectangle, rotation and tags a Case image does
/// (<see cref="CaseAssetCrop"/>, <see cref="CaseAssetRotation"/>,
/// <see cref="ImageTagAssignment"/>), keyed by its retained intake asset. When
/// the image becomes a Case document the preparation is copied onto that
/// occurrence, so the Case opens with the crop and tags already made.
/// </summary>
public sealed record PreCaseImagePreparation(
    Guid IntakeAssetId,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    IReadOnlyList<ImageTagAssignment> Tags,
    long Version)
{
    /// <summary>An image nobody has prepared: no rotation, the whole frame, no tags.</summary>
    public static PreCaseImagePreparation Original(Guid intakeAssetId) =>
        new(intakeAssetId, CaseAssetRotation.None, CaseAssetCrop.Full, [], 0);

    /// <summary>Whether a crop or rotation is recorded (the tile says Cropped).</summary>
    public bool IsPrepared => Rotation != CaseAssetRotation.None || !Crop.IsFull;
}

/// <summary>Apply (or, with no rotation and the full frame, Clear) the crop of one pre-Case image.</summary>
public sealed record SavePreCaseImageCropRequest(
    Guid IntakeAssetId,
    long ExpectedVersion,
    CaseAssetRotation Rotation,
    CaseAssetCrop Crop,
    ActionActor Actor,
    string OperationKey);

/// <summary>Put a tag on (<paramref name="Applied"/>) or take it off one pre-Case image.</summary>
public sealed record TagPreCaseImageRequest(
    Guid IntakeAssetId,
    Guid TagId,
    bool Applied,
    ActionActor Actor,
    string OperationKey);

public interface IPreCaseImagePreparationStore
{
    /// <summary>The recorded preparation of each named image; an image with none is absent.</summary>
    Task<IReadOnlyDictionary<Guid, PreCaseImagePreparation>> ListAsync(
        IReadOnlyCollection<Guid> intakeAssetIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the crop under the expected version, refusing an asset that is not
    /// a retained image. Idempotent on the operation key.
    /// </summary>
    Task<PreCaseImagePreparation> SaveCropAsync(
        SavePreCaseImageCropRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);

    /// <summary>Adds or removes one tag; idempotent on the operation key.</summary>
    Task<PreCaseImagePreparation> SetTagAsync(
        TagPreCaseImageRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken);
}

public interface IGetPreCaseImagePreparations
{
    Task<IReadOnlyDictionary<Guid, PreCaseImagePreparation>> ExecuteAsync(
        ActionActor actor,
        IReadOnlyCollection<Guid> intakeAssetIds,
        CancellationToken cancellationToken = default);
}

public interface ISavePreCaseImageCrop
{
    Task<PreCaseImagePreparation> ExecuteAsync(
        SavePreCaseImageCropRequest request,
        CancellationToken cancellationToken = default);
}

public interface ITagPreCaseImage
{
    Task<PreCaseImagePreparation> ExecuteAsync(
        TagPreCaseImageRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>The one owner of what a pre-Case crop or tag request may be.</summary>
public static class PreCaseImagePreparationPolicy
{
    public const int MaximumOperationKeyLength = 100;

    /// <summary>
    /// A pre-Case record is not a Case edit session (an Unidentified item has
    /// none), so the guard is the casework right, the image's own version and
    /// the operation key — not a Case lease.
    /// </summary>
    public static void Validate(SavePreCaseImageCropRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireCommon(request.IntakeAssetId, request.Actor, request.OperationKey);
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
        if (!Enum.IsDefined(request.Rotation))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Rotation, "An unrecognized rotation was supplied.");
        }

        ArgumentNullException.ThrowIfNull(request.Crop);
        request.Crop.Validate();
    }

    public static void Validate(TagPreCaseImageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireCommon(request.IntakeAssetId, request.Actor, request.OperationKey);
        if (request.TagId == Guid.Empty)
        {
            throw new ArgumentException("A tag is required.", nameof(request));
        }
    }

    private static void RequireCommon(Guid intakeAssetId, ActionActor actor, string operationKey)
    {
        if (intakeAssetId == Guid.Empty)
        {
            throw new ArgumentException("An image is required.", nameof(intakeAssetId));
        }

        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        if (operationKey.Trim().Length > MaximumOperationKeyLength)
        {
            throw new ArgumentException(
                $"The operation key must be {MaximumOperationKeyLength} characters or fewer.",
                nameof(operationKey));
        }
    }
}

public sealed class PreCaseImagePreparations(
    IPreCaseImagePreparationStore store,
    TimeProvider timeProvider) : IGetPreCaseImagePreparations, ISavePreCaseImageCrop, ITagPreCaseImage
{
    public Task<IReadOnlyDictionary<Guid, PreCaseImagePreparation>> ExecuteAsync(
        ActionActor actor,
        IReadOnlyCollection<Guid> intakeAssetIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(intakeAssetIds);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        return intakeAssetIds.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<Guid, PreCaseImagePreparation>>(new Dictionary<Guid, PreCaseImagePreparation>())
            : store.ListAsync(intakeAssetIds, cancellationToken);
    }

    public Task<PreCaseImagePreparation> ExecuteAsync(
        SavePreCaseImageCropRequest request,
        CancellationToken cancellationToken = default)
    {
        PreCaseImagePreparationPolicy.Validate(request);
        return store.SaveCropAsync(request, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<PreCaseImagePreparation> ExecuteAsync(
        TagPreCaseImageRequest request,
        CancellationToken cancellationToken = default)
    {
        PreCaseImagePreparationPolicy.Validate(request);
        return store.SetTagAsync(request, timeProvider.GetUtcNow(), cancellationToken);
    }
}
