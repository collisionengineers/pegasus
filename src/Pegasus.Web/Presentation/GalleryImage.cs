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
/// <paramref name="IntakeAssetId"/> marks an intake-linked image (image record, Triage Case,
/// Unidentified): the viewer then offers Crop (Apply / Clear / Cancel) and the
/// Tag select, and <paramref name="Preparation"/> is what is already recorded.
/// <paramref name="IntakeReceiptId"/> is the receipt that asset belongs to, so a
/// prepared image's tile can ask for its prepared rendering.
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
    Pegasus.Core.Intake.IncomingArtifactCustodyState? CustodyState = null)
{
    /// <summary>
    /// The tile source: a pre-Case image with a recorded crop or rotation shows
    /// the prepared region (the intake asset route's <c>size=thumb</c>, versioned
    /// so a new crop is a new address); otherwise the route's own rendering or
    /// the image itself. The viewer and Open file keep <see cref="Href"/>.
    /// </summary>
    public string TileHref =>
        IntakeReceiptId is { } receiptId && IntakeAssetId is { } assetId && Preparation is { IsPrepared: true } preparation
            ? $"/Received/{receiptId:D}/Asset/{assetId:D}?size={Pegasus.Core.Documents.CaseDocumentThumbnails.ThumbSizeToken}&v={preparation.Version}"
            : ThumbnailHref ?? Href;
}
