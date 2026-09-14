using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

/// <summary>One pre-Case image's tile: the retained intake asset of that receipt.</summary>
public sealed record PreCaseImageThumbnailQuery(Guid ReceiptId, Guid IntakeAssetId, ActionActor Actor);

/// <summary>
/// The tile of a pre-Case image with a recorded crop or rotation shows the
/// prepared region, exactly as a Case image's tile does
/// (<see cref="IReadCaseDocumentThumbnail"/>). The original is unchanged: Open
/// file and the viewer still read the retained bytes.
/// </summary>
public interface IReadPreCaseImageThumbnail
{
    /// <summary>
    /// The prepared rendering, or <c>null</c> when the image has no recorded
    /// crop or rotation, is not a renderable image, or could not be rendered —
    /// the caller then serves the original.
    /// </summary>
    Task<CaseDocumentThumbnail?> OpenAsync(
        PreCaseImageThumbnailQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ReadPreCaseImageThumbnail(
    IDownloadIntakeAsset downloadAsset,
    IPreCaseImagePreparationStore preparations,
    IRenderImageThumbnail renderer) : IReadPreCaseImageThumbnail
{
    public async Task<CaseDocumentThumbnail?> OpenAsync(
        PreCaseImageThumbnailQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Actor);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.ReceiptId == Guid.Empty || query.IntakeAssetId == Guid.Empty)
        {
            return null;
        }

        // The preparation first: an unprepared image needs no read and no render.
        var recorded = await preparations.ListAsync([query.IntakeAssetId], cancellationToken);
        if (!recorded.TryGetValue(query.IntakeAssetId, out var preparation) || !preparation.IsPrepared)
        {
            return null;
        }

        // The authorised, hash-verified read of that receipt's asset.
        var asset = await downloadAsset.ExecuteAsync(
            new DownloadIntakeAssetQuery(query.ReceiptId, query.IntakeAssetId, query.Actor),
            cancellationToken);
        if (asset is null || !CaseDocumentThumbnails.IsThumbnailable(asset.ContentType))
        {
            return null;
        }

        var rendered = await renderer.RenderAsync(asset.Content, preparation.Rotation, preparation.Crop, cancellationToken);
        return rendered is null
            ? null
            : new CaseDocumentThumbnail(
                new MemoryStream(rendered, writable: false),
                CaseDocumentThumbnails.MediaType,
                rendered.LongLength,
                asset.Sha256);
    }
}
