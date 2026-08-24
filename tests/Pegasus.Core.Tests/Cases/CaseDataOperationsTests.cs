using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class CaseDataOperationsTests
{
    private static readonly CaseWorkflowConfiguration Configuration = new(
        true,
        true,
        true,
        true,
        "test-case-workflow",
        7);

    [Fact]
    public void AccidentCircumstancesKeepTheBlankLineAboveTheDamageArea()
    {
        // ENG-015: this is the one case text field that keeps its line
        // structure, because EVA is sent the labelled damage-area block below
        // the prose verbatim. Every other text field still collapses.
        var normalized = CaseDataPolicy.Normalize(new(
            AccidentCircumstances: "The insured reversed into the claimant's vehicle.\n"
                + "\n"
                + "Damage Area: rear",
            ClaimantName: "Mrs   Caroline\nReynolds"));

        Assert.Equal(
            "The insured reversed into the claimant's vehicle.\n\nDamage Area: rear",
            normalized.AccidentCircumstances);
        Assert.Equal("Mrs Caroline Reynolds", normalized.ClaimantName);
    }

    [Fact]
    public void AccidentCircumstancesCollapseWhitespaceWithinAndBetweenLines()
    {
        var normalized = CaseDataPolicy.Normalize(new(
            AccidentCircumstances: "\n\n  Prose   with    gaps  \n\n\n\nDamage Area:   rear  \n\n"));

        Assert.Equal("Prose with gaps\n\nDamage Area: rear", normalized.AccidentCircumstances);
    }

    [Fact]
    public void CompletenessPolicyDoesNotTreatUnconfirmedValuesAsDefinitive()
    {
        var evaluation = CaseCompletenessPolicy.Evaluate(
            new(true, true, false, false),
            Configuration);

        Assert.False(evaluation.SatisfiesPolicy);
        Assert.Equal("test-case-workflow", evaluation.PolicyKey);
        Assert.Equal(7, evaluation.PolicyVersion);
    }

    [Fact]
    public async Task ConfirmCompletenessRequiresStaffActorAndActiveLeaseMaterial()
    {
        var store = new RecordingStore();
        var command = new ConfirmCompleteness(store, new FixedConfiguration(Configuration));
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<ArgumentException>(() => command.ExecuteAsync(
            new(
                Guid.NewGuid(),
                0,
                staff,
                "confirm-completeness",
                "Reviewed current evidence",
                " ",
                new(true, true, true, true)),
            CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => command.ExecuteAsync(
            new(
                Guid.NewGuid(),
                0,
                ActionActor.SystemWorker("worker"),
                "confirm-completeness-system",
                "Reviewed current evidence",
                "lease",
                new(true, true, true, true)),
            CancellationToken.None));

        Assert.Null(store.ConfirmedRequest);
    }

    [Fact]
    public void NormalizeRequiresInspectionAddressAndModeTogether()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(InspectionAddress: "1 Test Street, London")));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(InspectionMode: CaseInspectionMode.PhysicalAddress)));
    }

    [Fact]
    public void NormalizeRequiresTheExactValueForImageBasedAssessmentMode()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "1 Test Street, London",
                InspectionMode: CaseInspectionMode.ImageBasedAssessment)));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "image based assessment",
                InspectionMode: CaseInspectionMode.ImageBasedAssessment)));

        var normalized = CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "Image Based Assessment",
                InspectionMode: CaseInspectionMode.ImageBasedAssessment));
        Assert.Equal("Image Based Assessment", normalized.InspectionAddress);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, normalized.InspectionMode);
    }

    [Fact]
    public void NormalizeRejectsTheImageBasedAssessmentValueAsAPhysicalAddress()
    {
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "Image Based Assessment",
                InspectionMode: CaseInspectionMode.PhysicalAddress)));
        Assert.Throws<InvalidOperationException>(() => CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "IMAGE BASED ASSESSMENT",
                InspectionMode: CaseInspectionMode.PhysicalAddress)));

        var normalized = CaseDataPolicy.Normalize(
            new(
                InspectionAddress: "5 Repairer Way, Leeds",
                InspectionMode: CaseInspectionMode.PhysicalAddress));
        Assert.Equal("5 Repairer Way, Leeds", normalized.InspectionAddress);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, normalized.InspectionMode);
    }

    [Fact]
    public async Task SaveCaseNormalizesExplicitConfirmedValuesWithoutAnIdentityField()
    {
        var store = new RecordingStore();
        var command = new SaveCase(store);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var request = new SaveCaseRequest(
            Guid.NewGuid(),
            4,
            staff,
            "save-case",
            "Confirmed reviewed values",
            "lease",
            new(
                ClaimantName: "  Jane   Example ",
                VehicleRegistration: " ab 12 cde "));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => command.ExecuteAsync(request, CancellationToken.None));

        Assert.NotNull(store.SavedRequest);
        Assert.Equal("Jane Example", store.SavedRequest.Data.ClaimantName);
        Assert.Equal("AB12CDE", store.SavedRequest.Data.VehicleRegistration);
        Assert.Equal(request.CaseId, store.SavedRequest.CaseId);
    }

    private sealed class FixedConfiguration(CaseWorkflowConfiguration configuration)
        : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(configuration);
    }

    private sealed class RecordingStore : ICaseDataStore
    {
        public ConfirmCompletenessRequest? ConfirmedRequest { get; private set; }
        public SaveCaseRequest? SavedRequest { get; private set; }

        public Task<CaseDataProjection?> GetAsync(
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult<CaseDataProjection?>(null);

        public Task<CaseDataProjection> ConfirmCompletenessAsync(
            ConfirmCompletenessRequest request,
            CaseCompletenessEvaluation evaluation,
            CancellationToken cancellationToken)
        {
            ConfirmedRequest = request;
            throw new NotSupportedException();
        }

        public Task<CaseDataProjection> SaveAsync(
            SaveCaseRequest request,
            CancellationToken cancellationToken)
        {
            SavedRequest = request;
            throw new NotSupportedException();
        }
    }
}
