using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The two views of an Inspection + Audit Case once it has its Audit (v29
/// option 4): the Audit view, the default, is the work the Case is on; the
/// Inspection view reads the primary work and never edits. Every read that
/// depends on the view takes <see cref="WorkSelector"/>; the aside's Views
/// card, the Report's sent Inspection line and the Files Audit folder chip
/// read the members here. A Case without an Audit has one work, so the view
/// changes nothing on it.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>The <c>view</c> query value that addresses the Inspection view.</summary>
    public const string InspectionViewKey = "inspection";

    /// <summary>
    /// Which view the request addresses: <c>?view=inspection</c>, else the
    /// default (the Audit once one exists).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public string? ViewFilter { get; set; }

    /// <summary>The work every view-dependent read addresses.</summary>
    public CaseWorkSelector WorkSelector =>
        string.Equals(ViewFilter?.Trim(), InspectionViewKey, StringComparison.OrdinalIgnoreCase)
            ? CaseWorkSelector.Primary
            : CaseWorkSelector.Current;

    /// <summary>The Case's works, from whichever frame this response read.</summary>
    public CaseWorkSet? Works => Case?.Frame.Works ?? SectionFrame?.Works;

    /// <summary>The read-only Inspection view of a Case that has its Audit.</summary>
    public bool IsInspectionView => WorkSelector == CaseWorkSelector.Primary && Works?.HasAudit == true;

    /// <summary>
    /// The <c>view</c> route value the record's own read links carry: the
    /// Inspection view's while it is shown, else none, so every other link
    /// lands on the default view.
    /// </summary>
    public string? ViewRoute => IsInspectionView ? InspectionViewKey : null;

    /// <summary>
    /// The view a GET handler's redirect returns to. Those handlers read no
    /// frame, so the request's own view decides; writes never carry it.
    /// </summary>
    private string? RequestedView => WorkSelector == CaseWorkSelector.Primary ? InspectionViewKey : null;

    /// <summary>
    /// The Inspection's own current generation, read once the Case has its
    /// Audit: the Audit view's sent Inspection line and the Inspection view's
    /// report card. <see cref="CurrentReportGeneration"/> stays the current
    /// work's.
    /// </summary>
    public CaseReportGenerationRecord? InspectionReportGeneration { get; private set; }

    /// <summary>
    /// The Inspection report is still to be sent although the Case has its
    /// Audit: the Inspection view generates, prepares, sends and marks it sent
    /// on its own work, without touching the Case's state (operator,
    /// 1 October 2026).
    /// </summary>
    public bool InspectionReportAwaitsSend =>
        IsInspectionView && Works is { Primary.ReportSentEvidence: null };

    /// <summary>The work the Report section's delivery reads address: the Inspection's while its report awaits sending.</summary>
    public CaseWorkSelector ReportWorkSelector =>
        InspectionReportAwaitsSend ? CaseWorkSelector.Primary : CaseWorkSelector.Current;

    /// <summary>
    /// The Inspection report's readiness once the Case has its Audit and that
    /// report awaits sending; null otherwise. The Audit's readiness stays in
    /// <see cref="ReportDraftPreparation"/>, which the Next action reads.
    /// </summary>
    public AssessmentReportDraftPreparation? InspectionReportDraftPreparation { get; private set; }

    /// <summary>Why the Inspection report cannot be generated now, or null when it can; null too where it is not offered.</summary>
    public string? InspectionReportDraftCondition { get; private set; }

    /// <summary>Whether a posted <c>view</c> value names the Inspection view.</summary>
    public static bool IsInspectionViewRoute(string? view) =>
        string.Equals(view?.Trim(), InspectionViewKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>The work a Report command posted from <paramref name="view"/> addresses.</summary>
    public static CaseWorkSelector ReportWorkOf(string? view) =>
        IsInspectionViewRoute(view) ? CaseWorkSelector.Primary : CaseWorkSelector.Current;

    /// <summary>The view a Report command posted from <paramref name="view"/> returns to.</summary>
    public static string? ViewRouteOf(string? view) =>
        IsInspectionViewRoute(view) ? InspectionViewKey : null;

    /// <summary>The Audit report's reference, <c>a.{Case/PO}</c>.</summary>
    public string? AuditReportReference => Case is { } details
        ? CaseReferenceFormat.ReportReference(details.Workflow.Identity, CaseWorkKind.Audit)
        : null;

    /// <summary>
    /// "{Case/PO} · Sent {date}": the Inspection's sent report as the Report
    /// section states it in either view. Null without an Audit.
    /// </summary>
    public string? InspectionSentLine
    {
        get
        {
            if (Case is not { } details || Works is not { HasAudit: true } works)
            {
                return null;
            }

            var reference = details.Workflow.Identity.Reference;
            return works.Primary.ReportSentEvidence is { } sent
                ? $"{reference} · {CaseWorkspaceLabels.Frame.Sent} {OperatorLabels.OfficeTime(sent.SentAtUtc)}"
                : reference;
        }
    }

    /// <summary>
    /// The Overview's Report sent fact: the Inspection's own evidence in the
    /// Inspection view (Create audit moves it onto the primary work), else
    /// the workflow's.
    /// </summary>
    public ApprovedMailboxReportSentEvidence? ShownReportSentEvidence => IsInspectionView
        ? Works?.Primary.ReportSentEvidence
        : CurrentWorkflow?.ReportSentEvidence;
}
