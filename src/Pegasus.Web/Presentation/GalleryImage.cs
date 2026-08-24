namespace Pegasus.Web.Presentation;

/// <summary>
/// One gallery tile. <paramref name="Href"/> is the authorised URL the preview
/// reads; <paramref name="DownloadHref"/> is the authorised URL that saves the
/// file. They differ for a case document, which needs an explicit inline flag
/// to preview and the plain route to download, and coincide for a retained
/// receipt image, which is already served inline (DOCS-011).
/// <paramref name="MediaType"/> chooses the preview element.
/// </summary>
public sealed record GalleryImage(
    string Href,
    string DownloadHref,
    string FileName,
    string MediaType);
