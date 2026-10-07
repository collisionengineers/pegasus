using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The two views of an Inspection + Audit Case once it has its Audit (v29
/// option 4): the Audit view, the default, is the work the Case is on; the
/// Inspection view reads and edits the primary work (operator, 2 October
/// 2026). Every read that depends on the view takes <see cref="WorkSelector"/>,
/// and every write posted from a view carries it back (a hidden <c>view</c>
/// field), writes that view's work and returns to it; the aside's Views card
/// and the Files Audit folder chip read the members here. A Case without an
/// Audit has one work, so the view changes nothing on it.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>The <c>view</c> query value that addresses the Inspection view.</summary>
    public const string InspectionViewKey = "inspection";

    /// <summary>
    /// Which view the request addresses: <c>?view=inspection</c>, or a post's
    /// <c>view</c> field, else the default (the Audit once one exists).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public string? ViewFilter { get; set; }

    /// <summary>The work every view-dependent read addresses.</summary>
    public CaseWorkSelector WorkSelector => WorkOf(ViewFilter);

    /// <summary>The Case's works, from whichever frame this response read.</summary>
    public CaseWorkSet? Works => Case?.Frame.Works ?? SectionFrame?.Works;

    /// <summary>The Inspection view of a Case that has its Audit.</summary>
    public bool IsInspectionView => WorkSelector == CaseWorkSelector.Primary && Works?.HasAudit == true;

    /// <summary>
    /// The <c>view</c> value the record's own links and forms carry: the
    /// Inspection view's while it is shown, else none, so every other link
    /// and post lands on the default view.
    /// </summary>
    public string? ViewRoute => IsInspectionView ? InspectionViewKey : null;

    /// <summary>Whether a posted <c>view</c> value names the Inspection view.</summary>
    public static bool IsInspectionViewRoute(string? view) =>
        string.Equals(view?.Trim(), InspectionViewKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>The work a command posted from <paramref name="view"/> writes.</summary>
    public static CaseWorkSelector WorkOf(string? view) =>
        IsInspectionViewRoute(view) ? CaseWorkSelector.Primary : CaseWorkSelector.Current;

    /// <summary>The view a command posted from <paramref name="view"/> returns to.</summary>
    public static string? ViewRouteOf(string? view) =>
        IsInspectionViewRoute(view) ? InspectionViewKey : null;

    /// <summary>The Audit report's reference, <c>a.{Case/PO}</c>.</summary>
    public string? AuditReportReference => Case is { } details
        ? CaseReferenceFormat.ReportReference(details.Workflow.Identity, CaseWorkKind.Audit)
        : null;

    /// <summary>
    /// The Overview's Report sent fact: the Inspection's own evidence in the
    /// Inspection view (Create audit moves it onto the primary work), else
    /// the workflow's.
    /// </summary>
    public ApprovedMailboxReportSentEvidence? ShownReportSentEvidence => IsInspectionView
        ? Works?.Primary.ReportSentEvidence
        : CurrentWorkflow?.ReportSentEvidence;
}
