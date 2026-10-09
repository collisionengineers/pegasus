using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Case's images in their own window (operator, 9 October 2026): the
/// read-mode Files tiles and the same full-screen viewer, opened on the
/// image Pop out was pressed on, so the photographs can stay on a second
/// screen while the Case is worked on. It holds no edit lease and posts
/// nothing: crop, tag and In report stay on the Case record (FRD-14: a
/// second window of the same holder shares the Case lease, so this one
/// never takes it).
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class ImagesModel(
    IGetCaseFilesSection getCaseFilesSection,
    ICaseAssetPreparationQueries caseAssetPreparationQueries) : StaffPageModel
{
    public Guid CaseId { get; private set; }

    public string Reference { get; private set; } = string.Empty;

    /// <summary>The readable images, in the Images tab's order.</summary>
    public IReadOnlyList<CaseFile> Images { get; private set; } = [];

    /// <summary>The image the viewer opens on, when the address names one of the images.</summary>
    public Guid? StartOccurrenceId { get; private set; }

    private IReadOnlyList<CaseAssetPreparation> _preparations = [];

    public async Task<IActionResult> OnGetAsync(Guid id, Guid? image, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var files = await getCaseFilesSection.ExecuteAsync(new(id, actor), cancellationToken);
        if (files is null)
        {
            return NotFound();
        }

        CaseId = files.Frame.Workflow.CaseId;
        Reference = files.Frame.Summary.Reference;
        _preparations = await caseAssetPreparationQueries.ListForCaseAsync(CaseId, cancellationToken);
        var places = CaseAssetPreparationPolicy.ForReport(_preparations).ToDictionary(item => item.OccurrenceId);
        Images = CaseFileAddresses.ReportOrdered(
            CaseFiles.Current(files.Documents).Where(file =>
                CaseFileAddresses.IsCaseImage(file)
                && file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed),
            places);
        StartOccurrenceId = image is { } wanted && Images.Any(file => file.Occurrence.Id == wanted) ? wanted : null;
        return Page();
    }

    public string PreviewUrl(CaseFile file) => CaseFileAddresses.Preview(CaseId, file);

    public string DownloadUrl(CaseFile file) => CaseFileAddresses.Download(CaseId, file);

    public string ThumbnailUrl(CaseFile file) =>
        CaseFileAddresses.Thumbnail(
            CaseId,
            file,
            _preparations.FirstOrDefault(item => item.OccurrenceId == file.Occurrence.Id));
}
