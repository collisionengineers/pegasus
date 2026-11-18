using System.Globalization;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one place a Case file's preview, thumbnail and download addresses are
/// composed, and the rule for which files are the Case's images. The Case
/// record (v26 § Files, § Image viewer) and the images window
/// (<c>/Cases/{id}/Images</c>) both read it, so a tile on either page names
/// the same authorised route.
/// </summary>
public static class CaseFileAddresses
{
    /// <summary>
    /// Whether a file is one of the Case's images: the image role and a media
    /// type the tile can render. SVG stays a document because it is never
    /// displayed from this origin.
    /// </summary>
    public static bool IsCaseImage(CaseFile file) =>
        file.Occurrence.SemanticRole == DocumentSemanticRole.Image
        && (file.Version.MediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || file.Version.MediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The Images tab's tile order: the report's own order first, then the
    /// images the report does not use in their existing document order.
    /// </summary>
    public static IReadOnlyList<CaseFile> ReportOrdered(
        IEnumerable<CaseFile> images,
        IReadOnlyDictionary<Guid, PreparedReportImage> places) =>
    [
        .. images.OrderBy(file => places.TryGetValue(file.Occurrence.Id, out var image)
            ? image.Order
            : int.MaxValue)
    ];

    /// <summary>The inline preview the viewer reads (the original bytes, no history row).</summary>
    public static string Preview(Guid caseId, CaseFile file) =>
        Preview(caseId, file.Occurrence.Id, file.Version.Id);

    /// <summary>The inline preview of one occurrence's version, as <see cref="Preview(Guid, CaseFile)"/>.</summary>
    public static string Preview(Guid caseId, Guid occurrenceId, Guid versionId) =>
        $"/Cases/{caseId:D}/Documents/{occurrenceId:D}/Download?versionId={versionId:D}&inline=true";

    /// <summary>The audited download.</summary>
    public static string Download(Guid caseId, CaseFile file) =>
        Download(caseId, file.Occurrence.Id, file.Version.Id);

    /// <summary>The audited download of one occurrence's version, as <see cref="Download(Guid, CaseFile)"/>.</summary>
    public static string Download(Guid caseId, Guid occurrenceId, Guid versionId) =>
        $"/Cases/{caseId:D}/Documents/{occurrenceId:D}/Download?versionId={versionId:D}";

    /// <summary>
    /// The tile's derived rendering. It carries the preparation version so a
    /// saved crop is a new address rather than a week-cached earlier crop.
    /// </summary>
    public static string Thumbnail(Guid caseId, CaseFile file, CaseAssetPreparation? preparation) =>
        Preview(caseId, file)
        + "&size=" + CaseDocumentThumbnails.ThumbSizeToken
        + "&prep=" + (preparation?.PreparationVersion ?? 0).ToString(CultureInfo.InvariantCulture)
        + "&renderer=" + CaseDocumentThumbnails.RendererIdentity;

    /// <summary>
    /// The PDF rendering the viewer shows for a Word document. It names the
    /// rendering, so a changed renderer is a new address rather than a
    /// week-cached earlier rendering.
    /// </summary>
    public static string PdfRendition(Guid caseId, CaseFile file) =>
        PdfRendition(caseId, file.Occurrence.Id, file.Version.Id);

    /// <summary>The PDF rendering of one occurrence's Word version, as <see cref="PdfRendition(Guid, CaseFile)"/>.</summary>
    public static string PdfRendition(Guid caseId, Guid occurrenceId, Guid versionId) =>
        Preview(caseId, occurrenceId, versionId) + "&renderer=" + CaseDocumentPreviews.RenditionIdentity;

    /// <summary>The images window for a Case, opened on <paramref name="occurrenceId"/> when given.</summary>
    public static string ImagesWindow(Guid caseId, Guid? occurrenceId = null) =>
        $"/Cases/{caseId:D}/Images" + (occurrenceId is { } start ? $"?image={start:D}" : string.Empty);
}
