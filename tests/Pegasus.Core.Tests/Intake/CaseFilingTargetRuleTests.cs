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

    /// <summary>
    /// A photo link's kept paperwork and video, linked where its record
    /// merged, file wherever the photos went, as a member of staff's
    /// association does: a report-sent Case and a Triage Case included, an
    /// archived Case never (operator, 10 November 2026; R844).
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportSent)]
    public void ALinkThatFollowsARecordsMergeFilesInAnyState(CaseLifecycleState state)
    {
        var facts = Facts(isStaffAssociation: false, state) with { FollowsRecordMerge = true };

        Assert.Equal(CaseFilingTargetDisposition.Current, CaseFilingTargetRule.Decide(facts));
        Assert.Equal(
            CaseFilingTargetDisposition.NotApplicable,
            CaseFilingTargetRule.Decide(facts with { Workflow = facts.Workflow! with { IsArchived = true } }));
    }

    [Theory]
    [InlineData(true, false, true, CaseFilingTargetDisposition.Current)]
    [InlineData(false, true, true, CaseFilingTargetDisposition.Current)]
    [InlineData(false, false, true, CaseFilingTargetDisposition.NotApplicable)]
    [InlineData(false, true, false, CaseFilingTargetDisposition.NotApplicable)]
    public void ATriageCaseTakesAStaffLinkOrOneThatFollowsARecordsMerge(
        bool isStaffAssociation,
        bool followsRecordMerge,
        bool hasTriage,
        CaseFilingTargetDisposition expected)
    {
        var facts = new CaseFilingTargetFacts(
            isStaffAssociation,
            IsOriginReceipt: false,
            Workflow: null,
            hasTriage,
            followsRecordMerge);

        Assert.Equal(expected, CaseFilingTargetRule.Decide(facts));
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
