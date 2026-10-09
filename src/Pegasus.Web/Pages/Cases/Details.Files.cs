using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

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

            var correspondence = (FilesSection?.CorrespondenceEmails ?? [])
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

    /// <summary>The Images tab's tile order (<see cref="CaseFileAddresses.ReportOrdered"/>).</summary>
    public IReadOnlyList<CaseFile> ReportOrderedCaseImageFiles(IReadOnlyDictionary<Guid, PreparedReportImage> places) =>
        CaseFileAddresses.ReportOrdered(CaseImageFiles, places);

    /// <summary>
    /// Each image the report prints, by occurrence: its place and how it
    /// prints. The tile's order number is this place, so an image nobody has
    /// ordered shows where it prints.
    /// </summary>
    public IReadOnlyDictionary<Guid, PreparedReportImage> ReportImagesByOccurrence =>
        CaseAssetPreparationPolicy.ForReport(AssetPreparations).ToDictionary(image => image.OccurrenceId);

    /// <summary>Whether a file is one of the Case's images (<see cref="CaseFileAddresses.IsCaseImage"/>).</summary>
    public static bool IsCaseImage(CaseFile file) => CaseFileAddresses.IsCaseImage(file);

    /// <summary>The report preparation this image carries, if the Case has one for it.</summary>
    public CaseAssetPreparation? PreparationFor(Guid occurrenceId) =>
        AssetPreparations.FirstOrDefault(item => item.OccurrenceId == occurrenceId);

    /// <summary>
    /// U5a: an image is croppable whenever this browser holds the Case edit
    /// lease and the image has a preparation record, not only in report
    /// preparation and not only for an Engineer.
    /// </summary>
    public bool MayPrepareImages => CanEditCaseData;

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
    public string PreviewUrl(CaseFile file) => CaseFileAddresses.Preview(CurrentCaseId, file);

    /// <summary>The audited download.</summary>
    public string DownloadUrl(CaseFile file) => CaseFileAddresses.Download(CurrentCaseId, file);

    /// <summary>The tile's derived rendering, by its preparation version.</summary>
    public string ThumbnailUrl(CaseFile file) =>
        CaseFileAddresses.Thumbnail(CurrentCaseId, file, PreparationFor(file.Occurrence.Id));

    private Guid CurrentCaseId => FilesSection?.Frame.Workflow.CaseId ?? Case!.Workflow.CaseId;
}
