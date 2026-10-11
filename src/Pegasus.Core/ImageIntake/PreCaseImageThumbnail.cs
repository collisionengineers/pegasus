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
/// What one tile read produced: the tile when the image rendered, and the
/// authorised, hash-verified original it read either way, so a caller that
/// serves the original instead does not read it again. Both are <c>null</c>
/// when the asset is not found.
/// </summary>
public sealed record PreCaseImageRead(PreCaseImageThumbnail? Tile, IntakeSourceDownload? Original)
    : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Tile?.DisposeAsync() ?? ValueTask.CompletedTask;
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
    /// The rendering, or no <see cref="PreCaseImageRead.Tile"/> when the image
    /// is not a renderable image or could not be rendered — the caller then
    /// serves <see cref="PreCaseImageRead.Original"/>. An image
    /// with nothing recorded renders whole and reports
    /// <see cref="PreCaseImagePreparation.NoPreparationVersion"/>.
    /// </summary>
    Task<PreCaseImageRead> OpenAsync(
        PreCaseImageThumbnailQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ReadPreCaseImageThumbnail(
    IDownloadIntakeAsset downloadAsset,
    IPreCaseImagePreparationStore preparations,
    IRenderImageThumbnail renderer) : IReadPreCaseImageThumbnail
{
    public async Task<PreCaseImageRead> OpenAsync(
        PreCaseImageThumbnailQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Actor);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        if (query.ReceiptId == Guid.Empty || query.IntakeAssetId == Guid.Empty)
        {
            return new PreCaseImageRead(null, null);
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
            return new PreCaseImageRead(null, asset);
        }

        var rendered = await renderer.RenderAsync(asset.Content, preparation.Rotation, preparation.Crop, cancellationToken);
        return new PreCaseImageRead(
            rendered is null
                ? null
                : new PreCaseImageThumbnail(
                    new CaseDocumentThumbnail(
                        new MemoryStream(rendered, writable: false),
                        CaseDocumentThumbnails.MediaType,
                        rendered.LongLength,
                        asset.Sha256),
                    preparation.TileVersion),
            asset);
    }
}
