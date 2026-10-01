using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// FRD-13 "Cancellation messages": an open Case's Next action names the newest
/// linked message whose current classification is a cancellation; a closed or
/// archived Case names none.
/// </summary>
public sealed class CaseCancellationNoticeTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.Query)]
    public void AnOpenCaseCanShowACancellation(CaseLifecycleState state) =>
        Assert.True(CaseCancellationNotice.Applies(Workflow(state)));

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CreatedInError)]
    public void AClosedCaseShowsNoCancellation(CaseLifecycleState state) =>
        Assert.False(CaseCancellationNotice.Applies(Workflow(state)));

    [Fact]
    public void AnArchivedCaseShowsNoCancellation() =>
        Assert.False(CaseCancellationNotice.Applies(Workflow(CaseLifecycleState.ReportPreparation) with
        {
            Archive = new(Now, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]), "Archived")
        }));

    private static CaseWorkflowRecord Workflow(CaseLifecycleState state) => new(
        CaseId, new(CaseId, "QDOS", 2026, 1, "QDOS260001"), state, null, null, null, null, null, null, null, 1);
}
