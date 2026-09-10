using Pegasus.Core.Documents;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The report-image cards shared by Files and Report. Their values are staged
/// in the record's one Case form; this view is intentionally presentation-only.
/// </summary>
public sealed record ReportImagePreparationView(
    string Section,
    bool MayPrepare,
    IReadOnlyList<CaseAssetPreparation> Items,
    IReadOnlyDictionary<Guid, string> FileNames,
    IReadOnlyDictionary<Guid, string> PreviewUrls)
{
    public static ReportImagePreparationView Files(DetailsModel page) =>
        Create(page, "files", page.AssetPreparations);

    public static ReportImagePreparationView Report(DetailsModel page)
    {
        var byOccurrence = page.AssetPreparations.ToDictionary(item => item.OccurrenceId);
        return Create(page, "report", [.. page.PreparedReportImages
            .Select(image => byOccurrence.GetValueOrDefault(image.OccurrenceId))
            .OfType<CaseAssetPreparation>()]);
    }

    private static ReportImagePreparationView Create(
        DetailsModel page,
        string section,
        IReadOnlyList<CaseAssetPreparation> items)
    {
        ArgumentNullException.ThrowIfNull(page);
        var details = page.Case!;
        var files = CaseFiles.Live(details.Documents);
        return new(
            section,
            // U5a: preparation follows the Case edit lease, not the
            // engineering window. Gating it on CanEditEngineering is why a
            // Review-state Case offered no Crop anywhere.
            page.CanEditCaseData && details.Data is not null,
            items,
            files.ToDictionary(file => file.Occurrence.Id, file => file.Version.FileName),
            files.ToDictionary(
                file => file.Occurrence.Id,
                file => $"/Cases/{details.Workflow.CaseId:D}/Documents/{file.Occurrence.Id:D}/Download?versionId={file.Version.Id:D}&inline=true"));
    }
}
