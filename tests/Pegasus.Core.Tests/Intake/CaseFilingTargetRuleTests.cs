using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// Whether a receipt's retained evidence files on its associated Case now:
/// a member of staff's association never waits for an editor, whoever holds
/// the lease; the pipeline's own waits while a lease is live (R727).
/// </summary>
public sealed class CaseFilingTargetRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(true, CaseLifecycleState.Review, CaseFilingTargetDisposition.Current)]
    [InlineData(true, CaseLifecycleState.ReportSent, CaseFilingTargetDisposition.Current)]
    [InlineData(false, CaseLifecycleState.Review, CaseFilingTargetDisposition.Deferred)]
    public void ALiveEditLeaseDefersOnlyThePipelinesFiling(
        bool isStaffAssociation,
        CaseLifecycleState state,
        CaseFilingTargetDisposition expected)
    {
        Assert.Equal(
            expected,
            CaseFilingTargetRule.Decide(Facts(isStaffAssociation, state, Now.AddMinutes(5)), Now));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ALapsedLeaseDefersNothing(bool isStaffAssociation)
    {
        Assert.Equal(
            CaseFilingTargetDisposition.Current,
            CaseFilingTargetRule.Decide(
                Facts(isStaffAssociation, CaseLifecycleState.Review, Now.AddMinutes(-1)), Now));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnArchivedCaseNeverFilesUnderALease(bool isStaffAssociation)
    {
        var facts = Facts(isStaffAssociation, CaseLifecycleState.Review, Now.AddMinutes(5));

        Assert.Equal(
            CaseFilingTargetDisposition.NotApplicable,
            CaseFilingTargetRule.Decide(
                facts with { Workflow = facts.Workflow! with { IsArchived = true } }, Now));
    }

    private static CaseFilingTargetFacts Facts(
        bool isStaffAssociation,
        CaseLifecycleState state,
        DateTimeOffset? leaseExpiresAtUtc) =>
        new(
            isStaffAssociation,
            IsOriginReceipt: false,
            new CaseFilingWorkflowFacts(
                IsArchived: false,
                state,
                HasReportSentEvidence: state == CaseLifecycleState.ReportSent,
                leaseExpiresAtUtc),
            HasTriage: false);
}
