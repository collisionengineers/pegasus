using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Closure page: report approval is covered beside the workspace tests; these cover the
/// terminal outcomes, reopening through the destination gates, and archiving.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task ClosurePageBindsTerminalOutcomeReopenGatesAndArchive()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ICloseCase>(services, store);
            Substitute<IReopenCase>(services, store);
            Substitute<IArchiveCase>(services, store);
        });

        using var closed = await workspace.PostAsync(
            "Closure?handler=Close",
            workspace.MutationForm("close-case", "Provider withdrew the instruction", ("outcome", "ProviderCancelled")));
        using var reopenedToReview = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm(
                "reopen-review",
                "Provider reinstated the instruction",
                ("destination", "Review"),
                ("instructionsComplete", "true"),
                ("imagesComplete", "false"),
                ("instructionsReviewedByStaff", "true"),
                ("imagesReviewedByStaff", "false"),
                ("evidenceReference", "reopen-evidence-1")));
        using var reopenedToNotReady = await workspace.PostAsync(
            "Closure?handler=Reopen",
            workspace.MutationForm("reopen-not-ready", "Images still outstanding", ("destination", "NotReady")));
        using var archived = await workspace.PostAsync(
            "Closure?handler=Archive",
            workspace.MutationForm("archive-case", "Retention period reached"));

        AssertPrg(closed, store.CaseId);
        AssertPrg(reopenedToReview, store.CaseId);
        AssertPrg(reopenedToNotReady, store.CaseId);
        AssertPrg(archived, store.CaseId);

        var closure = Assert.Single(store.Closures);
        AssertClaimant(workspace, closure.Actor);
        Assert.Equal(store.CaseVersion, closure.ExpectedVersion);
        Assert.Equal(store.LeaseToken, closure.EditLeaseToken);
        Assert.Equal("close-case", closure.OperationKey);
        Assert.Equal("Provider withdrew the instruction", closure.Reason);
        Assert.Equal(CaseClosureOutcome.ProviderCancelled, closure.Outcome);

        Assert.Equal(2, store.Reopenings.Count);
        var toReview = store.Reopenings[0];
        AssertClaimant(workspace, toReview.Actor);
        Assert.Equal(store.CaseVersion, toReview.ExpectedVersion);
        Assert.Equal(store.LeaseToken, toReview.EditLeaseToken);
        Assert.Equal("reopen-review", toReview.OperationKey);
        Assert.Equal(CaseReopenDestination.Review, toReview.Destination);
        Assert.Equal(new CaseReadinessEvidence(true, false, true, false, "reopen-evidence-1"), toReview.Readiness);
        var toNotReady = store.Reopenings[1];
        Assert.Equal("reopen-not-ready", toNotReady.OperationKey);
        Assert.Equal(CaseReopenDestination.NotReady, toNotReady.Destination);
        Assert.Null(toNotReady.Readiness);

        var archive = Assert.Single(store.Archives);
        AssertClaimant(workspace, archive.Actor);
        Assert.Equal(store.CaseVersion, archive.ExpectedVersion);
        Assert.Equal(store.LeaseToken, archive.EditLeaseToken);
        Assert.Equal("archive-case", archive.OperationKey);
        Assert.Equal("Retention period reached", archive.Reason);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("The terminal case was archived and is now read-only.", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Closure?handler=Close",
            workspace.MutationForm("close-case-2", "Already closed", ("outcome", "PostReportComplete")));
    }

    private sealed partial class RecordingCaseDetailsStore :
        ICloseCase,
        IReopenCase,
        IArchiveCase
    {
        public List<CloseCaseRequest> Closures { get; } = [];
        public List<ReopenCaseRequest> Reopenings { get; } = [];
        public List<ArchiveCaseRequest> Archives { get; } = [];

        Task<CaseWorkflowRecord> ICloseCase.ExecuteAsync(
            CloseCaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            Closures.Add(request);
            return Task.FromResult(CreateWorkflow() with
            {
                State = CaseLifecycleState.ProviderCancelled,
                ClosureOutcome = request.Outcome
            });
        }

        Task<CaseWorkflowRecord> IReopenCase.ExecuteAsync(
            ReopenCaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            Reopenings.Add(request);
            return Task.FromResult(CreateWorkflow() with
            {
                State = request.Destination == CaseReopenDestination.Review
                    ? CaseLifecycleState.Review
                    : CaseLifecycleState.NotReady
            });
        }

        Task<CaseWorkflowRecord> IArchiveCase.ExecuteAsync(
            ArchiveCaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            Archives.Add(request);
            return Task.FromResult(CreateWorkflow() with
            {
                Archive = new(_now, request.Actor, request.Reason)
            });
        }
    }
}
