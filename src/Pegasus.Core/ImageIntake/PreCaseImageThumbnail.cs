using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

/// <summary>One pre-Case image's tile: the retained intake asset of that receipt.</summary>
public sealed record PreCaseImageThumbnailQuery(Guid ReceiptId, Guid IntakeAssetId, ActionActor Actor);

/// <summary>
/// A pre-Case image's rendering and the preparation version it drew
/// (<see cref="PreCaseImagePreparation.TileVersion"/>), so the route can tell a
/// current tile address from a stale one.
/// </summary>
public sealed record PreCaseImageThumbnail(CaseDocumentThumbnail Rendering, long PreparationVersion)
    : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Rendering.DisposeAsync();
}

/// <summary>
/// The tile of a pre-Case image is a rendering at tile size, exactly as a Case
/// image's tile is (<see cref="IReadCaseDocumentThumbnail"/>): the prepared
/// region when a crop or rotation is recorded, otherwise the whole frame. The
/// original is unchanged: Open file and the viewer still read the retained
/// bytes.
/// </summary>
public interface IReadPreCaseImageThumbnail
{
    /// <summary>
    /// The rendering, or <c>null</c> when the image is not a renderable image
    /// or could not be rendered — the caller then serves the original. An image
    /// with nothing recorded renders whole and reports
    /// <see cref="PreCaseImagePreparation.NoPreparationVersion"/>.
    /// </summary>
    Task<PreCaseImageThumbnail?> OpenAsync(
        PreCaseImageThumbnailQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ReadPreCaseImageThumbnail(
    IDownloadIntakeAsset downloadAsset,
    IPreCaseImagePreparationStore preparations,
    IRenderImageThumbnail renderer) : IReadPreCaseImageThumbnail
{
    public async Task<PreCaseImageThumbnail?> OpenAsync(
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

        // An image nobody has prepared, or whose crop was cleared, is drawn
        // whole: no rotation and the full frame.
        var recorded = await preparations.ListAsync([query.IntakeAssetId], cancellationToken);
        var preparation = recorded.GetValueOrDefault(query.IntakeAssetId)
            ?? PreCaseImagePreparation.Original(query.IntakeAssetId);

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
            : new PreCaseImageThumbnail(
                new CaseDocumentThumbnail(
                    new MemoryStream(rendered, writable: false),
                    CaseDocumentThumbnails.MediaType,
                    rendered.LongLength,
                    asset.Sha256),
                preparation.TileVersion);
    }
}
