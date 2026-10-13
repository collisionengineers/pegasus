using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// One step of a Case's Next action: its words, the section its link opens,
/// the blocker it names while the report is not ready, and whether it opens
/// the Actions menu's Create audit or Assign Engineer dialog, or is Resolve
/// query, rather than a section, or is the Case's open tasks, which the
/// Next action lists in its place (operator, 6 October 2026).
/// </summary>
public sealed record CaseNextActionStep(
    string Label,
    string SectionKey,
    AssessmentReadinessItem? Blocker = null,
    bool OpensCreateAudit = false,
    bool OpensAssignEngineer = false,
    bool ResolvesQuery = false,
    bool ListsOpenTasks = false);

/// <summary>
/// The one Next action of a Case: the Case page's aside states it and the
/// Cases quick detail shows it as the Case's current work, so both name the
/// same step (issue 896). Once the Case has its Audit, the Inspection view
/// states the Inspection report's own step instead (operator, 2 October 2026).
/// </summary>
public static class CaseNextAction
{
    /// <summary>
    /// The next permitted lifecycle action and the section it links to. In
    /// Review it is Assign Engineer, which opens the Actions menu's dialog
    /// (issue 1025). A Report sent Case with an open query resolves it
    /// (operator, 11 October 2026). With Engineer, while the report is not
    /// ready, it names the first blocker at the section that clears it, and
    /// at the Report section when that blocker has none, and carries that
    /// blocker. Once the report is stored, an Inspection + Audit Case without
    /// its Audit creates it next, whether or not the report is sent; any
    /// other Case sends the report, and once a send of it is accepted its
    /// step is its open tasks while it has any (operator, 6 October 2026),
    /// else none (operator, 12 October 2026).
    /// </summary>
    /// <param name="caseType">The Case's type: Create audit follows the stored report of an Inspection + Audit Case.</param>
    /// <param name="works">The Case's works; null reads as the primary work alone.</param>
    /// <param name="firstRequirement">The first outstanding requirement's words, if any.</param>
    /// <param name="reportBlockers">The report readiness items; empty when the report is ready or its readiness was not read.</param>
    /// <param name="blockerSection">The section that clears a blocker on this Case, or null.</param>
    /// <param name="reportSent">Whether a send of the current work's report was accepted: Pegasus's own send record, which the Sent evidence follows.</param>
    /// <param name="hasOpenTasks">Whether the Case has an open task; read once the report is sent.</param>
    public static CaseNextActionStep Of(
        CaseWorkflowRecord workflow,
        CaseType caseType,
        CaseWorkSet? works,
        string? firstRequirement,
        IReadOnlyList<AssessmentReadinessItem> reportBlockers,
        Func<AssessmentReadinessItem, string?> blockerSection,
        CaseReportGenerationRecord? currentReport,
        bool reportSent,
        bool hasOpenTasks = false)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(reportBlockers);
        ArgumentNullException.ThrowIfNull(blockerSection);
        if (BeforeTheReport(workflow, firstRequirement) is { } early)
        {
            return early;
        }
        if (workflow.State == CaseLifecycleState.ReportSent && workflow.HasOpenQuery)
        {
            return new(CaseWorkspaceLabels.Frame.ResolveQuery, "queries", ResolvesQuery: true);
        }
        return ReportStep(reportBlockers, blockerSection, currentReport)
            ?? AuditStep(workflow, caseType, works)
            ?? (workflow.ReportSentEvidence is not null || reportSent
                ? AfterTheSend(hasOpenTasks)
                : DeliveryStep);
    }

    /// <summary>
    /// The Inspection report's own step once the Case has its Audit, as the
    /// Inspection view states it: the Case's state is the Audit's, so no state
    /// gate applies; once a send of the Inspection report is accepted there
    /// is no step (operator, 2 and 12 October 2026), unless a later change
    /// made that report stale, when it is generated again.
    /// </summary>
    /// <param name="sentEvidence">The Inspection work's own Sent evidence, if any.</param>
    /// <param name="reportSent">Whether a send of the Inspection report was accepted.</param>
    public static CaseNextActionStep? OfPastWork(
        IReadOnlyList<AssessmentReadinessItem> reportBlockers,
        Func<AssessmentReadinessItem, string?> blockerSection,
        CaseReportGenerationRecord? report,
        ApprovedMailboxReportSentEvidence? sentEvidence,
        bool reportSent)
    {
        ArgumentNullException.ThrowIfNull(reportBlockers);
        ArgumentNullException.ThrowIfNull(blockerSection);
        return ReportStep(reportBlockers, blockerSection, report)
            ?? (sentEvidence is not null || reportSent ? null : DeliveryStep);
    }

    /// <summary>
    /// Whether the Next action reads the report's readiness and the current
    /// report; a caller that has not loaded them need not.
    /// </summary>
    public static bool ReadsTheReport(CaseWorkflowRecord workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return BeforeTheReport(workflow, null) is null
            && !(workflow.State == CaseLifecycleState.ReportSent && workflow.HasOpenQuery);
    }

    // The steps the Case takes before the report is its next concern.
    private static CaseNextActionStep? BeforeTheReport(CaseWorkflowRecord workflow, string? firstRequirement)
    {
        if (workflow.Archive is not null || CaseLifecycleRules.IsTerminal(workflow.State))
        {
            return new("None", "notes");
        }
        if (workflow.State == CaseLifecycleState.Review)
        {
            return new(CaseWorkspaceLabels.AssignEngineer, "overview", OpensAssignEngineer: true);
        }
        if (workflow.State is CaseLifecycleState.NotReady or CaseLifecycleState.Held)
        {
            return new(firstRequirement ?? OperatorLabels.CaseStage(workflow.State), "overview");
        }
        return null;
    }

    // What stands between a work and its stored report; null once the report
    // is stored, when Create audit or delivery is the step.
    private static CaseNextActionStep? ReportStep(
        IReadOnlyList<AssessmentReadinessItem> reportBlockers,
        Func<AssessmentReadinessItem, string?> blockerSection,
        CaseReportGenerationRecord? report)
    {
        if (reportBlockers.Count > 0)
        {
            // The step is the first blocker, linking to the section that
            // clears it, and to Report when it has none. It never counts the
            // rest: FRD-13 allows no summary. The Case page's aside draws it
            // in full and lists every blocker in its Report not ready card
            // (operator, 8 October 2026).
            var first = reportBlockers[0];
            return new(first.Requirement, blockerSection(first) ?? "report", first);
        }
        if (report is null || report.State == CaseReportGenerationState.Stale)
        {
            return new(CaseWorkspaceLabels.ReportDelivery.GenerateReport, "report");
        }
        // A report that is not stored cannot be delivered. One on its way to
        // Box is waited for; one never drawn, failed or not confirmed is
        // generated again.
        var reportFiling = report.Artifacts
            .FirstOrDefault(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport)
            ?.Filing;
        if (reportFiling != CaseReportArtifactFiling.Stored)
        {
            return new(
                reportFiling == CaseReportArtifactFiling.BeingStored
                    ? CaseWorkspaceLabels.ReportDelivery.WaitingForStorage
                    : CaseWorkspaceLabels.ReportDelivery.GenerateReport,
                "report");
        }
        return null;
    }

    private static CaseNextActionStep DeliveryStep =>
        new(CaseWorkspaceLabels.ReportDelivery.SendReport, "report");

    // Once the report is sent, the Case's open tasks are what is left to do;
    // the Next action lists them, each opening the Tasks dialog (operator,
    // 6 and 13 October 2026). With none, nothing is (operator, 12 October 2026).
    private static CaseNextActionStep AfterTheSend(bool hasOpenTasks) =>
        hasOpenTasks
            ? new(OperatorLabels.WorkCentre.OpenTasks, "overview", ListsOpenTasks: true)
            : new("None", "notes");

    // What follows the stored report of an Inspection + Audit Case that has
    // no Audit yet: Create audit, offered even without an Engineer, where the
    // item is greyed with that reason (operator, 11 and 12 October 2026). Null
    // on any other Case, which sends its report.
    private static CaseNextActionStep? AuditStep(CaseWorkflowRecord workflow, CaseType caseType, CaseWorkSet? works) =>
        AuditPolicy.Refusal(caseType, workflow, works) is null or AuditRefusal.NoAssignedEngineer
            ? new(CaseWorkspaceLabels.Frame.CreateAudit, "overview", OpensCreateAudit: true)
            : null;
}
