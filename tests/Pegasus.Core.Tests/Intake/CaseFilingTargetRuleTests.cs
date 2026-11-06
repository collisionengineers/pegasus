using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Whether a receipt's retained evidence files on its associated Case now.
/// The rule has no edit-lease input: no filing waits for an editor (R727,
/// R728); a member of staff's association files on any open Case, the
/// pipeline's only where automatic association is accepted.
/// </summary>
public sealed class CaseFilingTargetRuleTests
{
    [Theory]
    [InlineData(true, CaseLifecycleState.Review, CaseFilingTargetDisposition.Current)]
    [InlineData(true, CaseLifecycleState.ReportSent, CaseFilingTargetDisposition.Current)]
    [InlineData(false, CaseLifecycleState.Review, CaseFilingTargetDisposition.Current)]
    [InlineData(false, CaseLifecycleState.ReportSent, CaseFilingTargetDisposition.NotApplicable)]
    public void AStaffAssociationFilesInAnyStateAndThePipelinesOnlyWhereItIsAccepted(
        bool isStaffAssociation,
        CaseLifecycleState state,
        CaseFilingTargetDisposition expected)
    {
        Assert.Equal(expected, CaseFilingTargetRule.Decide(Facts(isStaffAssociation, state)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnArchivedCaseNeverFiles(bool isStaffAssociation)
    {
        var facts = Facts(isStaffAssociation, CaseLifecycleState.Review);

        Assert.Equal(
            CaseFilingTargetDisposition.NotApplicable,
            CaseFilingTargetRule.Decide(facts with { Workflow = facts.Workflow! with { IsArchived = true } }));
    }

    private static CaseFilingTargetFacts Facts(bool isStaffAssociation, CaseLifecycleState state) =>
        new(
            isStaffAssociation,
            IsOriginReceipt: false,
            new CaseFilingWorkflowFacts(
                IsArchived: false,
                state,
                HasReportSentEvidence: state == CaseLifecycleState.ReportSent),
            HasTriage: false);
}
