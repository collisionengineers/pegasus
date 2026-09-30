namespace Pegasus.Web.Presentation;

/// <summary>
/// One gallery tile. <paramref name="Href"/> is the authorised URL the preview
/// reads; <paramref name="DownloadHref"/> is the authorised URL that saves the
/// file. They differ for a case document, which needs an explicit inline flag
/// to preview and the plain route to download, and coincide for a retained
/// receipt image, which is already served inline.
/// <paramref name="MediaType"/> chooses the preview element.
///
/// <paramref name="ThumbnailHref"/> is the tile's own source when the route
/// serves a derived rendering: the tile is 135 px wide, so asking it for the
/// full photograph was the page load, sixty times over. The viewer
/// and the download keep <paramref name="Href"/> and
/// <paramref name="DownloadHref"/>. It is null where a route has no rendering,
/// and the tile then shows the full image as it always did.
///
/// <paramref name="IsStored"/> is false while the file is still on its way to
/// durable custody: there is nothing to render yet, so the tile says so rather
/// than pointing an <c>img</c> at a version that cannot be read.
///
/// <paramref name="CustodyState"/> supplies the operator-facing explanation
/// for an unavailable tile. It is null for callers that do not expose incoming
/// custody state.
///
/// <paramref name="IntakeAssetId"/> marks a pre-Case image (image record, Triage,
/// Unidentified): the viewer then offers Crop (Apply / Clear / Cancel) and the
/// Tag select, and <paramref name="Preparation"/> is what is already recorded.
/// <paramref name="IntakeReceiptId"/> is the receipt that asset belongs to, so
/// the image's tile can ask for its rendering.
/// <paramref name="ContentHash"/> is that asset's content hash, which names the
/// tile's address so the browser may keep it (<see cref="IntakeImageAddress"/>).
/// </summary>
public sealed record GalleryImage(
    string Href,
    string DownloadHref,
    string FileName,
    string MediaType,
    string? ThumbnailHref = null,
    bool IsStored = true,
    Guid? IntakeAssetId = null,
    Pegasus.Core.ImageIntake.PreCaseImagePreparation? Preparation = null,
    Guid? IntakeReceiptId = null,
    Pegasus.Core.Intake.IncomingArtifactCustodyState? CustodyState = null,
    string? ContentHash = null)
{
    /// <summary>
    /// The tile source: every pre-Case image that can be rendered shows a
    /// rendering at tile size (the intake asset route's <c>size=thumb</c>),
    /// prepared or not: the recorded region, or the whole frame when nothing is
    /// recorded. The address names the content, the preparation version and the
    /// renderer, so a new crop is a new address. Any other image shows the
    /// route's own rendering or the image itself. The viewer and Open file keep
    /// <see cref="Href"/>.
    /// </summary>
    public string TileHref =>
        IntakeReceiptId is { } receiptId
        && IntakeAssetId is { } assetId
        && Pegasus.Core.Documents.CaseDocumentThumbnails.IsThumbnailable(MediaType)
            ? IntakeImageAddress.Tile(
                receiptId,
                assetId,
                ContentHash,
                Preparation?.TileVersion ?? Pegasus.Core.ImageIntake.PreCaseImagePreparation.NoPreparationVersion)
            : ThumbnailHref ?? Href;
}
