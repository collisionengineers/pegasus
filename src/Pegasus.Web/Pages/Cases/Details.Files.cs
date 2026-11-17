using Pegasus.Core.Documents;
using Pegasus.Core.Lifecycle;
using Pegasus.Web.Presentation;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.AiWork;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Email;
using EstimateVatLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateVat;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using FrameLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame;
using Labels = Pegasus.Web.Presentation.OperatorLabels;
using EditorLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors;

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

    /// <summary>
    /// Whether one of the Case's images can be read: the images window is
    /// offered from Files' Pop out and the Damage markers step's View images
    /// once it is, and is absent, never disabled, before (operator, 9 October
    /// and 8 November 2026; R757).
    /// </summary>
    public bool HasReadableImage =>
        CaseImageFiles.Any(file => file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed);

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
    /// Whether the Case viewer offers Take snapshot on a video: any open,
    /// non-archived Case, in or out of Edit mode (R727, R964).
    /// </summary>
    public bool CanSnapshotVideos =>
        CurrentWorkflow is { Archive: null } workflow
        && !CaseLifecycleRules.IsTerminal(workflow.State);
    /// <summary>The inline preview the viewer reads (the original bytes, no history row).</summary>
    public string PreviewUrl(CaseFile file) => CaseFileAddresses.Preview(CurrentCaseId, file);

    /// <summary>The PDF rendering the viewer shows for a Word document (no history row).</summary>
    public string PdfRenditionUrl(CaseFile file) => CaseFileAddresses.PdfRendition(CurrentCaseId, file);

    /// <summary>The audited download.</summary>
    public string DownloadUrl(CaseFile file) => CaseFileAddresses.Download(CurrentCaseId, file);

    /// <summary>The tile's derived rendering, by its preparation version.</summary>
    public string ThumbnailUrl(CaseFile file, CaseAssetPreparation? preparation) =>
        CaseFileAddresses.Thumbnail(CurrentCaseId, file, preparation);

    private Guid CurrentCaseId => FilesSection?.Frame.Workflow.CaseId ?? Case!.Workflow.CaseId;

    /// <summary>
    /// The associated Vehicle images records whose photographs are still the
    /// record's own. Once a record's merge files them they are Case images,
    /// drawn once as tiles, and the record has no group here.
    /// </summary>
    public IReadOnlyList<ImageIntakeSummary> ImageIntakes { get; private set; } = [];

    /// <summary>
    /// The shared image-tag vocabulary, loaded only when the Files section
    /// renders and the operator holds the edit lease: a read-only visit's
    /// tiles draw their tags as chips alone, so they never ask for it.
    /// </summary>
    public IReadOnlyList<ImageTag> TagVocabulary { get; private set; } = [];

    /// <summary>
    /// The gallery entries for each associated Image-initiated Case, loaded
    /// only when the Files section's body is being rendered.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> ImagesByIntake { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<ImageIntakeImage>>();

    /// <summary>
    /// Every photo link whose items are on the Case, with the items, for the
    /// Files → Images story (FRD-28, FRD-16): one read with the Files body,
    /// none while photo capture is not composed.
    /// </summary>
    public IReadOnlyList<Pegasus.Core.PhotoCapture.CasePhotoCaptureStory> PhotoCaptureStories { get; private set; } = [];

    /// <summary>
    /// Whether a photo link on the Case still takes photos, so its story
    /// offers Cancel (R795) and, with nothing yet, Resend the link (R806). A
    /// link on an archived Case is dead (R793).
    /// </summary>
    public bool IsPhotoLinkLive(Pegasus.Core.PhotoCapture.PhotoCapture capture) =>
        Pegasus.Core.PhotoCapture.PhotoCaptureAccess.IsLive(capture, clock.GetUtcNow(), CurrentWorkflow?.Archive is not null);

    /// <summary>
    /// A photo link's derived status beside it on the Case (R806), the way
    /// the Photo capture lists derive it: from its record and what it still
    /// lacks, never stored.
    /// </summary>
    public Pegasus.Core.PhotoCapture.PhotoCaptureState PhotoLinkState(Pegasus.Core.PhotoCapture.CasePhotoCaptureStory story)
    {
        ArgumentNullException.ThrowIfNull(story);
        var photographs = story.Items.Count(item => Pegasus.Core.PhotoCapture.PhotoCaptureStory.IsPhotographKind(item.Kind));
        return Pegasus.Core.PhotoCapture.PhotoCaptureStatus.Evaluate(
            story.Capture, PhotoLinkMissing(story), photographs, clock.GetUtcNow(), CurrentWorkflow?.Archive is not null);
    }

    /// <summary>
    /// What a photo link on the Case still lacks, in the lists' words: what
    /// decides its status, and the Images bar's Missing line (v51 I; R948).
    /// </summary>
    public IReadOnlyList<Pegasus.Core.PhotoCapture.MissingCaptureItem> PhotoLinkMissing(Pegasus.Core.PhotoCapture.CasePhotoCaptureStory story)
    {
        ArgumentNullException.ThrowIfNull(story);
        var capture = story.Capture;
        return Pegasus.Core.PhotoCapture.PhotoCaptureRequiredItems.Evaluate(
            capture,
            Pegasus.Core.PhotoCapture.PhotoCaptureItemPresence.Of(story.Items),
            Pegasus.Core.PhotoCapture.PhotoCaptureRequiredItems.VinRequired(capture, story.StandingLinkIsRepairer, story.HandedOn));
    }

    /// <summary>The Files body's image-intake records, tag vocabulary and galleries.</summary>
    private sealed record FilesReads(
        IReadOnlyList<ImageIntakeSummary> ImageIntakes,
        IReadOnlyList<ImageTag> TagVocabulary,
        IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> ImagesByIntake,
        IReadOnlyList<Pegasus.Core.PhotoCapture.CasePhotoCaptureStory> PhotoCaptures);

    /// <summary>
    /// The Files body alone needs its image-intake and instruction-photo lists.
    /// Keeping those reads with that body prevents the initial record response
    /// and unrelated section fragments from preparing galleries the operator
    /// has not opened. The picker needs the whole vocabulary; a read-only visit
    /// draws chips only, so it does not ask for it.
    /// </summary>
    private async Task<FilesReads> ReadFilesAsync(
        Guid caseId,
        bool readsVocabulary,
        CancellationToken cancellationToken)
    {
        // The vocabulary is independent of the intakes and their images.
        var vocabulary = readsVocabulary
            ? readImageTagVocabulary.ListAsync(cancellationToken)
            : Task.FromResult<IReadOnlyList<ImageTag>>([]);
        var intakesAndImages = ReadIntakesAndImagesAsync(caseId, cancellationToken);
        // The photo links on the Case: one statement, with the Files body only
        // (FRD-28), so its images can stand in story order.
        var photoCaptures = photoCaptureQueries is not null
            && configuration is not null
            && Pegasus.Web.Presentation.ShellRail.PhotoCaptureEnabled(configuration)
                ? photoCaptureQueries.ListForCaseAsync(caseId, cancellationToken)
                : Task.FromResult<IReadOnlyList<Pegasus.Core.PhotoCapture.CasePhotoCaptureStory>>([]);
        await Task.WhenAll(vocabulary, intakesAndImages, photoCaptures);
        return new(intakesAndImages.Result.Intakes, vocabulary.Result, intakesAndImages.Result.Images, photoCaptures.Result);
    }

    private async Task<(
        IReadOnlyList<ImageIntakeSummary> Intakes,
        IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> Images)>
        ReadIntakesAndImagesAsync(Guid caseId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImageIntakeSummary> intakes =
        [
            .. (await imageIntakeQueries.ListForCaseAsync(caseId, cancellationToken))
                .Where(intake => !intake.PhotographsAreCaseImages)
        ];
        var images = await imageIntakeQueries.ListImagesAsync(
            intakes.Select(intake => intake.Id).ToArray(),
            cancellationToken);
        return (intakes, images);
    }

    private void ApplyFiles(FilesReads files)
    {
        ImageIntakes = files.ImageIntakes;
        TagVocabulary = files.TagVocabulary;
        ImagesByIntake = files.ImagesByIntake;
        PhotoCaptureStories = files.PhotoCaptures;
    }
}
