using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Workflow page: hold, release, and start-work are covered beside the workspace tests; these
/// cover return-to-Review, Engineer assignment and finding, and the linked replacement.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<ITransitionCase>(services, store);
            Substitute<IAssignCaseEngineer>(services, store);
            Substitute<IRecordEngineerFinding>(services, store);
            Substitute<ICreateLinkedReplacement>(services, store);
        });
        var engineerId = Guid.NewGuid();
        (string Name, string Value)[] readiness =
        [
            ("instructionsComplete", "true"),
            ("imagesComplete", "true"),
            ("instructionsReviewedByStaff", "true"),
            ("imagesReviewedByStaff", "false"),
            ("evidenceReference", "review-evidence-1")
        ];

        using var returned = await workspace.PostAsync(
            "Workflow?handler=ReturnToReview",
            workspace.MutationForm("return-to-review", "Images arrived", readiness));
        using var assigned = await workspace.PostAsync(
            "Workflow?handler=AssignEngineer",
            workspace.MutationForm("assign-engineer", "Engineer available", [("engineerId", engineerId.ToString("D")), .. readiness]));
        using var found = await workspace.PostAsync(
            "Workflow?handler=RecordEngineerFinding",
            workspace.MutationForm("record-finding", "Inspection complete", ("assessment", "TotalLoss")));
        using var replaced = await workspace.PostAsync(
            "Workflow?handler=CreateLinkedReplacement",
            workspace.MutationForm("create-replacement", "Wrong principal", ("replacementPrincipalCode", "ACME")));

        AssertPrg(returned, store.CaseId);
        AssertPrg(assigned, store.CaseId);
        AssertPrg(found, store.CaseId);
        AssertPrg(replaced, store.CaseId);
        var expectedReadiness = new CaseReadinessEvidence(true, true, true, false, "review-evidence-1");

        var transition = Assert.Single(store.Transitions);
        AssertClaimant(workspace, transition.Actor);
        Assert.Equal(store.CaseVersion, transition.ExpectedVersion);
        Assert.Equal(store.LeaseToken, transition.EditLeaseToken);
        Assert.Equal("return-to-review", transition.OperationKey);
        Assert.Equal("Images arrived", transition.Reason);
        Assert.Equal(CaseTransitionDestination.Review, transition.Destination);
        Assert.Equal(expectedReadiness, transition.Readiness);

        var assignment = Assert.Single(store.EngineerAssignments);
        AssertClaimant(workspace, assignment.Actor);
        Assert.Equal(store.CaseVersion, assignment.ExpectedVersion);
        Assert.Equal(store.LeaseToken, assignment.EditLeaseToken);
        Assert.Equal("assign-engineer", assignment.OperationKey);
        Assert.Equal(engineerId, assignment.EngineerId);
        Assert.Equal(expectedReadiness, assignment.Readiness);

        var finding = Assert.Single(store.EngineerFindings);
        AssertClaimant(workspace, finding.Actor);
        Assert.Equal(store.CaseVersion, finding.ExpectedVersion);
        Assert.Equal(store.LeaseToken, finding.EditLeaseToken);
        Assert.Equal("record-finding", finding.OperationKey);
        Assert.Equal(AuditAssessment.TotalLoss, finding.Assessment);

        var replacement = Assert.Single(store.LinkedReplacements);
        AssertClaimant(workspace, replacement.Actor);
        Assert.Equal(store.CaseVersion, replacement.ExpectedVersion);
        Assert.Equal(store.LeaseToken, replacement.EditLeaseToken);
        Assert.Equal("create-replacement", replacement.OperationKey);
        Assert.Equal("Wrong principal", replacement.Reason);
        Assert.Equal("ACME", replacement.ReplacementPrincipalCode);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("Replacement case ACME3100001 was allocated and linked.", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Workflow?handler=RecordEngineerFinding",
            workspace.MutationForm("record-finding-2", "Second look", ("assessment", "Repairable")));
        await AssertLostLeaseClearsEditModeAsync(
            workspace,
            "Workflow?handler=ReturnToReview",
            workspace.MutationForm("return-to-review-2", "Lease gone", readiness));
    }

    private sealed partial class RecordingCaseDetailsStore :
        IAssignCaseEngineer,
        IRecordEngineerFinding,
        ICreateLinkedReplacement
    {
        public List<AssignCaseEngineerRequest> EngineerAssignments { get; } = [];
        public List<RecordEngineerFindingRequest> EngineerFindings { get; } = [];
        public List<CreateLinkedReplacementRequest> LinkedReplacements { get; } = [];

        Task<CaseWorkflowRecord> IAssignCaseEngineer.ExecuteAsync(
            AssignCaseEngineerRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EngineerAssignments.Add(request);
            return Task.FromResult(CreateWorkflow() with { AssignedEngineerId = request.EngineerId });
        }

        Task<CaseIdentity> IRecordEngineerFinding.ExecuteAsync(
            RecordEngineerFindingRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            EngineerFindings.Add(request);
            return Task.FromResult(CreateWorkflow().Identity);
        }

        Task<CaseAcceptanceOutcome> ICreateLinkedReplacement.ExecuteAsync(
            CreateLinkedReplacementRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            LinkedReplacements.Add(request);
            var replacementId = Guid.NewGuid();
            return Task.FromResult(new CaseAcceptanceOutcome(
                new(replacementId, request.ReplacementPrincipalCode, 2031, 1, $"{request.ReplacementPrincipalCode}3100001"),
                CaseInitialState.NotReady,
                CaseCustodyState.Pending,
                Guid.NewGuid(),
                IsDuplicate: false));
        }
    }
}
