using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class PostReportQueryRulesTests
{
    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete, false, PostReportQueryEntry.EnterQuery)]
    [InlineData(CaseLifecycleState.PostReportComplete, true, PostReportQueryEntry.CompleteWithObservedReply)]
    [InlineData(CaseLifecycleState.Query, false, PostReportQueryEntry.None)]
    [InlineData(CaseLifecycleState.ReportPreparation, false, PostReportQueryEntry.None)]
    [InlineData(CaseLifecycleState.PostReport, false, PostReportQueryEntry.None)]
    [InlineData(CaseLifecycleState.PrincipalCancelled, false, PostReportQueryEntry.None)]
    public void APostReportMessageMovesOnlyACompletedCase(
        CaseLifecycleState state,
        bool replyObserved,
        PostReportQueryEntry expected) =>
        Assert.Equal(expected, PostReportQueryRules.OnPostReportLinked(state, replyObserved));

    [Theory]
    [InlineData(CaseLifecycleState.Query, false, false, true)]
    [InlineData(CaseLifecycleState.Query, true, false, false)]
    [InlineData(CaseLifecycleState.Query, false, true, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, false, false, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, false, false, false)]
    public void TheLastPostReportMessageLeavingBeforeAnyReplyWithdrawsTheQuery(
        CaseLifecycleState state,
        bool replyObserved,
        bool otherPostReportLinked,
        bool expected) =>
        Assert.Equal(expected, PostReportQueryRules.ShouldWithdraw(state, replyObserved, otherPostReportLinked));
}
