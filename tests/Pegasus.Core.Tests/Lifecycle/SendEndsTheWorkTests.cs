using Pegasus.Core.Cases;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

/// <summary>
/// Operator, 13 October 2026: an Inspection + Audit Case with no Audit yet stays
/// With Engineer when its Inspection report is sent; only the Audit report's send
/// or a closure takes it off the Cases page, and a query stays raisable meanwhile.
/// </summary>
public sealed class SendEndsTheWorkTests
{
    [Theory]
    [InlineData("inspection", CaseType.Inspection, false, true)]
    [InlineData("audit", CaseType.Audit, false, true)]
    [InlineData("inspection-audit-with-audit", CaseType.InspectionAndAudit, true, true)]
    [InlineData("inspection-audit-no-audit", CaseType.InspectionAndAudit, false, false)]
    public void OnlyAnInspectionAndAuditCaseWithoutItsAuditStaysWithEngineerAfterASend(
        string name,
        CaseType caseType,
        bool hasAudit,
        bool endsTheWork)
    {
        Assert.NotEmpty(name);
        Assert.Equal(endsTheWork, CaseLifecycleRules.SendEndsTheWork(caseType, hasAudit));
    }

    [Theory]
    [InlineData("report-sent", CaseLifecycleState.ReportSent, false, CaseType.Inspection, false, true)]
    [InlineData("window", CaseLifecycleState.ReportPreparation, true, CaseType.InspectionAndAudit, false, true)]
    [InlineData("window-no-evidence", CaseLifecycleState.ReportPreparation, false, CaseType.InspectionAndAudit, false, false)]
    [InlineData("returned-to-engineer", CaseLifecycleState.ReportPreparation, true, CaseType.Inspection, false, false)]
    [InlineData("returned-after-audit", CaseLifecycleState.ReportPreparation, true, CaseType.InspectionAndAudit, true, false)]
    [InlineData("review", CaseLifecycleState.Review, true, CaseType.InspectionAndAudit, false, false)]
    public void TheReportIsSentOnReportSentOrInTheInspectionAndAuditWindow(
        string name,
        CaseLifecycleState state,
        bool hasSentEvidence,
        CaseType caseType,
        bool hasAudit,
        bool expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, CaseLifecycleRules.ReportIsSent(state, hasSentEvidence, caseType, hasAudit));
    }
}
