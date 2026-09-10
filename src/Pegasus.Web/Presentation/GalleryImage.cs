namespace Pegasus.Web.Presentation;

/// <summary>
/// One gallery tile. <paramref name="Href"/> is the authorised URL the preview
/// reads; <paramref name="DownloadHref"/> is the authorised URL that saves the
/// file. They differ for a case document, which needs an explicit inline flag
/// to preview and the plain route to download, and coincide for a retained
/// receipt image, which is already served inline (DOCS-011).
/// <paramref name="MediaType"/> chooses the preview element.
///
/// <paramref name="ThumbnailHref"/> is the tile's own source when the route
/// serves a derived rendering: the tile is 135 px wide, so asking it for the
/// full photograph was the page load, sixty times over (DOCS-015). The viewer
/// and the download keep <paramref name="Href"/> and
/// <paramref name="DownloadHref"/>. It is null where a route has no rendering,
/// and the tile then shows the full image as it always did.
///
/// <paramref name="IsStored"/> is false while the file is still on its way to
/// durable custody: there is nothing to render yet, so the tile says so rather
/// than pointing an <c>img</c> at a version that cannot be read.
/// </summary>
public sealed record GalleryImage(
    string Href,
    string DownloadHref,
    string FileName,
    string MediaType,
    string? ThumbnailHref = null,
    bool IsStored = true);
