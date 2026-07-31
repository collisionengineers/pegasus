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
