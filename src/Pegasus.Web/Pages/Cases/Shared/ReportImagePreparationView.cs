using System.Globalization;
using Microsoft.AspNetCore.Html;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// B08: everything the shared report-image preparation partial renders, for
/// one section of the Case record. Files and Report both read the one loaded
/// preparation set, so the cards, the controls and the posted field names are
/// identical in both places; only <see cref="Section"/> — the section the
/// forms post back to, so the redirect returns the operator where they acted
/// — differs.
/// </summary>
/// <param name="Section">The <c>?section=</c> key the cards live in.</param>
/// <param name="CaseId">The case the commands address.</param>
/// <param name="ExpectedVersion">The case version every command carries.</param>
/// <param name="LeaseToken">The viewer's edit lease, absent in read-only view.</param>
/// <param name="MayPrepare">Whether the controls are rendered at all.</param>
/// <param name="Items">The cards this section shows, in its own order.</param>
/// <param name="Supporting">
/// Every Supporting image in report order, so Move up and Move down name the
/// same neighbours from either section.
/// </param>
/// <param name="FileNames">The live file name of each occurrence.</param>
public sealed record ReportImagePreparationView(
    string Section,
    Guid CaseId,
    long ExpectedVersion,
    string? LeaseToken,
    bool MayPrepare,
    IReadOnlyList<CaseAssetPreparation> Items,
    IReadOnlyList<CaseAssetPreparation> Supporting,
    IReadOnlyDictionary<Guid, string> FileNames)
{
    /// <summary>
    /// The shape of the edit a drop submits, written on the card itself: the
    /// drag enhancement reads these and posts the same SaveAssetPreparation
    /// command Move up and Move down post, so it needs no endpoint and no
    /// value the server does not already render.
    /// </summary>
    /// <remarks>
    /// The whole set is written together or not at all. Razor drops an
    /// attribute whose value expression is null, but never a <c>data-</c> one
    /// — it would leave every read-only card carrying empty hooks — so the
    /// condition is answered here, once, instead of per attribute.
    /// </remarks>
    public IHtmlContent DragAttributes(CaseAssetPreparation item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!MayPrepare || item.Role != CaseAssetReportRole.Supporting)
        {
            return HtmlString.Empty;
        }

        var attributes = new HtmlContentBuilder();
        Attribute(attributes, "draggable", "true");
        Attribute(attributes, "data-preparation-version", Number(item.PreparationVersion));
        Attribute(attributes, "data-report-role", item.Role.ToString());
        Attribute(attributes, "data-report-order", item.Order is { } order ? Number(order) : string.Empty);
        Attribute(attributes, "data-report-rotation", Number((int)item.Rotation));
        Attribute(attributes, "data-crop-left", Number(item.Crop.Left));
        Attribute(attributes, "data-crop-top", Number(item.Crop.Top));
        Attribute(attributes, "data-crop-width", Number(item.Crop.Width));
        Attribute(attributes, "data-crop-height", Number(item.Crop.Height));
        return attributes;
    }

    /// <summary>The name is ours; only the value can carry markup, so only it is encoded.</summary>
    private static void Attribute(HtmlContentBuilder attributes, string name, string value) =>
        attributes
            .AppendHtml(" ")
            .AppendHtml(name)
            .AppendHtml("=\"")
            .Append(value)
            .AppendHtml("\"");

    private static string Number(IFormattable value) =>
        value.ToString(null, CultureInfo.InvariantCulture);

    /// <summary>
    /// Where <paramref name="item"/> sits in the Supporting sequence, or -1
    /// when it is not one. Move up and Move down name their neighbour from
    /// this one sequence, so both sections offer the same move.
    /// </summary>
    public int Place(CaseAssetPreparation item)
    {
        ArgumentNullException.ThrowIfNull(item);
        for (var index = 0; index < Supporting.Count; index++)
        {
            if (Supporting[index].OccurrenceId == item.OccurrenceId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The Files section's cards: every occurrence, Not used included.</summary>
    public static ReportImagePreparationView Files(DetailsModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Create(page, "files", page.AssetPreparations);
    }

    /// <summary>
    /// The Report section's cards: the same preparations, in Core's own report
    /// order and without the images the report does not use. The order is read
    /// from <see cref="DetailsModel.PreparedReportImages"/> rather than sorted
    /// again here, so the rule lives only in Core.
    /// </summary>
    public static ReportImagePreparationView Report(DetailsModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var byOccurrence = page.AssetPreparations.ToDictionary(item => item.OccurrenceId);
        return Create(
            page,
            "report",
            [.. page.PreparedReportImages
                .Select(image => byOccurrence.GetValueOrDefault(image.OccurrenceId))
                .OfType<CaseAssetPreparation>()]);
    }

    private static ReportImagePreparationView Create(
        DetailsModel page,
        string section,
        IReadOnlyList<CaseAssetPreparation> items)
    {
        var details = page.Case!;
        var leaseToken = page.LeaseToken;
        return new(
            section,
            details.Summary.CaseId,
            details.Workflow.Version,
            leaseToken,
            !string.IsNullOrWhiteSpace(leaseToken)
                && details.Workflow.Archive is null
                && !page.AssessmentIsReadOnly,
            items,
            [.. page.AssetPreparations
                .Where(candidate => candidate.Role == CaseAssetReportRole.Supporting)
                .OrderBy(candidate => candidate.Order ?? int.MaxValue)],
            CaseFiles.Live(details.Documents)
                .ToDictionary(file => file.Occurrence.Id, file => file.Version.FileName));
    }
}
