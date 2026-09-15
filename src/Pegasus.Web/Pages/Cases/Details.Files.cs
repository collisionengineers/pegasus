using System.Globalization;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Files section's members (v26 § Files, § Image viewer): which of the
/// Case's files are images, the one place a tile's preview, thumbnail and
/// download addresses are composed, and the preparation each image carries.
/// Nothing here decides custody or authority: the routes do that again.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>
    /// Every current file that is not an image and is not listed as
    /// correspondence: the Documents tab. An uploaded email is retained as a
    /// correspondence row over the same bytes, and is read from that tab.
    /// </summary>
    public IReadOnlyList<CaseFile> CaseDocumentFiles
    {
        get
        {
            if (Case is null && FilesSection is null)
            {
                return [];
            }

            var correspondence = (FilesSection?.QueryEmails ?? [])
                .Select(email => email.SourceSha256)
                .Where(hash => !string.IsNullOrWhiteSpace(hash))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var documents = FilesSection?.Documents ?? Case!.Documents;
            return [.. CaseFiles.Current(documents).Where(file =>
                !IsCaseImage(file) && !correspondence.Contains(file.Version.Sha256))];
        }
    }

    /// <summary>Every current image file, confirmed or still arriving: the Images tab and the strips.</summary>
    public IReadOnlyList<CaseFile> CaseImageFiles =>
        Case is null && FilesSection is null
            ? []
            : [.. CaseFiles.Current(FilesSection?.Documents ?? Case!.Documents).Where(IsCaseImage)];

    /// <summary>The images whose bytes can be read: the viewer's set and the Report strip.</summary>
    public IReadOnlyList<CaseFile> ViewableCaseImages =>
        [.. CaseImageFiles.Where(file => file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed)];

    /// <summary>
    /// Whether a file is one of the Case's images: the image role and a media
    /// type the tile can render. SVG stays a document because it is never
    /// displayed from this origin.
    /// </summary>
    public static bool IsCaseImage(CaseFile file) =>
        file.Occurrence.SemanticRole == DocumentSemanticRole.Image
        && (file.Version.MediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || file.Version.MediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase));

    /// <summary>The report preparation this image carries, if the Case has one for it.</summary>
    public CaseAssetPreparation? PreparationFor(Guid occurrenceId) =>
        AssetPreparations.FirstOrDefault(item => item.OccurrenceId == occurrenceId);

    /// <summary>
    /// U5a: an image is croppable whenever this browser holds the Case edit
    /// lease and the image has a preparation record, not only in report
    /// preparation and not only for an Engineer.
    /// </summary>
    public bool MayPrepareImages => CanEditCaseData && FilesSection is not null;

    /// <summary>
    /// Which files the viewer displays over the page: images, PDFs and the two
    /// admitted video media types; everything else is a download. Restates the
    /// inline rule the document route applies.
    /// </summary>
    public static bool IsViewable(string mediaType) =>
        MediaTypeHeaderValue.TryParse(mediaType, out var parsed)
        && (parsed.MediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || parsed.MediaType.Equals(IntakeUploadFilePolicy.Mp4MediaType, StringComparison.OrdinalIgnoreCase)
            || parsed.MediaType.Equals(IntakeUploadFilePolicy.MovMediaType, StringComparison.OrdinalIgnoreCase)
            || (parsed.Type.Equals("image", StringComparison.OrdinalIgnoreCase)
                && !parsed.MediaType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase)));

    /// <summary>The inline preview the viewer reads (the original bytes, no history row).</summary>
    public string PreviewUrl(CaseFile file) =>
        $"/Cases/{CurrentCaseId:D}/Documents/{file.Occurrence.Id:D}/Download?versionId={file.Version.Id:D}&inline=true";

    /// <summary>The audited download.</summary>
    public string DownloadUrl(CaseFile file) =>
        $"/Cases/{CurrentCaseId:D}/Documents/{file.Occurrence.Id:D}/Download?versionId={file.Version.Id:D}";

    /// <summary>
    /// The tile's derived rendering. It carries the preparation version so a
    /// saved crop is a new address rather than a week-cached earlier crop.
    /// </summary>
    public string ThumbnailUrl(CaseFile file)
    {
        var preparation = PreparationFor(file.Occurrence.Id);
        var address = PreviewUrl(file) + "&size=" + CaseDocumentThumbnails.ThumbSizeToken;
        return address + "&prep="
            + (preparation?.PreparationVersion ?? 0).ToString(CultureInfo.InvariantCulture);
    }

    private Guid CurrentCaseId => FilesSection?.Frame.Workflow.CaseId ?? Case!.Workflow.CaseId;
}
