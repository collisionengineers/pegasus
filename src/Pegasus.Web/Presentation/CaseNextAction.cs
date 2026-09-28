using Pegasus.Core.Assessment;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one Next action of a Case: the Case page's aside states it and the
/// Cases quick detail shows it as the Case's current work, so both name the
/// same step (issue 896).
/// </summary>
public static class CaseNextAction
{
    /// <summary>
    /// The next permitted lifecycle action and the section it links to. With
    /// Engineer, while the report is not ready, it names the first blocker at
    /// the section that clears it, and at the Report section when that blocker
    /// has none, and carries that blocker. Delivery is the next action only
    /// once the report is stored.
    /// </summary>
    /// <param name="firstRequirement">The first outstanding requirement's words, if any.</param>
    /// <param name="reportBlockers">The report readiness items; empty when the report is ready or its readiness was not read.</param>
    /// <param name="blockerSection">The section that clears a blocker on this Case, or null.</param>
    public static (string Label, string SectionKey, AssessmentReadinessItem? Blocker) Of(
        CaseWorkflowRecord workflow,
        string? firstRequirement,
        IReadOnlyList<AssessmentReadinessItem> reportBlockers,
        Func<AssessmentReadinessItem, string?> blockerSection,
        CaseReportGenerationRecord? currentReport,
        CaseReportDeliveryPreparationRecord? deliveryPreparation)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(reportBlockers);
        ArgumentNullException.ThrowIfNull(blockerSection);
        if (workflow.Archive is not null || CaseLifecycleRules.IsTerminal(workflow.State))
        {
            return ("None", "notes", null);
        }
        if (workflow.State == CaseLifecycleState.Review)
        {
            return (CaseWorkspaceLabels.HandToEngineer, "overview", null);
        }
        if (workflow.State is CaseLifecycleState.NotReady or CaseLifecycleState.Held)
        {
            return (firstRequirement ?? OperatorLabels.CaseStage(workflow.State), "overview", null);
        }
        if (reportBlockers.Count > 0)
        {
            // One line: the first blocker and how many follow, linking to the
            // section that clears the first (FRD-13). The Report section lists
            // every blocker with its own link, and is the target when the
            // first has no section.
            var first = reportBlockers[0];
            return (reportBlockers.Count > 1
                    ? $"{first.Requirement} · {reportBlockers.Count - 1} more"
                    : first.Requirement,
                blockerSection(first) ?? "report",
                first);
        }
        if (currentReport is null || currentReport.State == CaseReportGenerationState.Stale)
        {
            return (CaseWorkspaceLabels.ReportDelivery.GenerateReport, "report", null);
        }
        // A report that is not stored cannot be delivered. One on its way to
        // Box is waited for; one never drawn, failed or not confirmed is
        // generated again.
        var reportFiling = currentReport.Artifacts
            .FirstOrDefault(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport)
            ?.Filing;
        if (reportFiling != CaseReportArtifactFiling.Stored)
        {
            return (reportFiling == CaseReportArtifactFiling.BeingStored
                    ? CaseWorkspaceLabels.ReportDelivery.WaitingForStorage
                    : CaseWorkspaceLabels.ReportDelivery.GenerateReport,
                "report",
                null);
        }
        if (workflow.ReportSentEvidence is not null)
        {
            return ("Mark completed", "overview", null);
        }
        if (deliveryPreparation is not null)
        {
            return (CaseWorkspaceLabels.ReportDelivery.SendPreparedReport, "report", null);
        }
        return (CaseWorkspaceLabels.ReportDelivery.PrepareDelivery, "report", null);
    }
}
