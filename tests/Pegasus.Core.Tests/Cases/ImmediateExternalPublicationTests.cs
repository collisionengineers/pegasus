using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class ImmediateExternalPublicationTests
{
    private sealed class RecordingTriagePairing : ITriageCasePairing
    {
        public List<Guid> CaseIds { get; } = [];
        public Task<TriageCasePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken)
        {
            CaseIds.Add(caseId);
            return Task.FromResult(new TriageCasePairingResult(0, 0, 0));
        }
        public Task<TriageCasePairingResult> PairTriageAsync(Guid triageId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TriageCasePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task AcceptancePublishesTheCustodyWorkCreatedByItsCommittedTransaction()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(workItemId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher,
            new RecordingTriagePairing());

        var result = await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    /// <summary>
    /// Acceptance also publishes the automatic vehicle lookup its transaction
    /// enqueued, so DVLA/MOT evidence arrives with the new Case rather than on
    /// the Worker's next reconciliation sweep (FRD-06 D34).
    /// </summary>
    [Fact]
    public async Task AcceptancePublishesTheVehicleLookupItsTransactionEnqueued()
    {
        var custodyWorkId = Guid.NewGuid();
        var vehicleLookupWorkId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(custodyWorkId, vehicleLookupWorkId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher,
            new RecordingTriagePairing());

        await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.Equal([custodyWorkId, vehicleLookupWorkId], publisher.WorkItemIds);
    }

    /// <summary>
    /// An acceptance that enqueued no lookup — no unambiguous registration, or
    /// lookups not composed — publishes only its custody work.
    /// </summary>
    [Fact]
    public async Task AcceptanceWithNoEnqueuedLookupPublishesOnlyCustody()
    {
        var custodyWorkId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(custodyWorkId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher,
            new RecordingTriagePairing());

        await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.Equal([custodyWorkId], publisher.WorkItemIds);
    }

    [Fact]
    public async Task DuplicateAcceptanceRetriesPairingWithoutRepublishingAcceptanceCustody()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var pairing = new RecordingPairing();
        var triagePairing = new RecordingTriagePairing();
        var acceptance = new AcceptIntake(new AcceptanceStore(workItemId),
            new ConfigurationStore(), new InspectionModeStore(), publisher, triagePairing, pairing);
        var request = AcceptanceRequest();

        var first = await acceptance.ExecuteAsync(request, CancellationToken.None);
        var replay = await acceptance.ExecuteAsync(request, CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Identity, replay.Identity);
        Assert.Equal([first.Identity.CaseId, first.Identity.CaseId], pairing.CaseIds);
        Assert.Equal([first.Identity.CaseId, first.Identity.CaseId], triagePairing.CaseIds);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    [Fact]
    public async Task ReplacementPublishesTheCustodyWorkCreatedByItsCommittedTransaction()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var replacement = new CreateLinkedReplacement(new ReplacementStore(workItemId), publisher);

        var result = await replacement.ExecuteAsync(
            new(
                Guid.NewGuid(),
                0,
                Staff(),
                "replacement-1",
                "Correct source identity.",
                "lease-token",
                "QDOS"),
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    private static AcceptIntakeRequest AcceptanceRequest() =>
        new(
            Guid.NewGuid(),
            0,
            Staff(),
            "acceptance-1",
            CaseType.Inspection,
            "QDOS",
            new(true, true));

    private static ActionActor Staff() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class RecordingPublisher : ICommittedExternalWorkPublisher
    {
        public List<Guid> WorkItemIds { get; } = [];

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            WorkItemIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptanceStore(Guid workItemId, Guid? vehicleLookupWorkId = null)
        : ICaseAcceptanceStore
    {
        private readonly CaseAcceptanceOutcome outcome = Outcome(workItemId, vehicleLookupWorkId);
        private bool accepted;

        public Task<CaseAcceptanceOutcome> AcceptAsync(
            CaseAcceptanceRequest request,
            CancellationToken cancellationToken)
        {
            var result = outcome with { IsDuplicate = accepted };
            accepted = true;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingPairing : IImageIntakeCasePairing
    {
        public List<Guid> CaseIds { get; } = [];

        public Task<ImageIntakePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken)
        {
            CaseIds.Add(caseId);
            return Task.FromResult(CaseIds.Count == 1
                ? new ImageIntakePairingResult(1, 0, 1, nameof(IntakeAssociationConflictException))
                : new ImageIntakePairingResult(1, 1, 0));
        }

        public Task<ImageIntakePairingResult> PairRegisteredReceiptAsync(Guid receiptId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ImageIntakePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ReplacementStore(Guid workItemId) : ILinkedCaseReplacementStore
    {
        public Task<CaseAcceptanceOutcome> CreateAsync(
            CreateLinkedReplacementRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Outcome(workItemId));
    }

    private sealed class ConfigurationStore : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseWorkflowConfiguration("test", 1));
    }

    private sealed class InspectionModeStore : IProviderInspectionModeStore
    {
        public Task<CaseInspectionMode?> GetForPrincipalAsync(
            string principalCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseInspectionMode?>(null);
    }

    private static CaseAcceptanceOutcome Outcome(
        Guid workItemId,
        Guid? vehicleLookupWorkId = null) =>
        new(
            new CaseIdentity(Guid.NewGuid(), "QDOS", 2031, 1, "QDS31001"),
            CaseInitialState.Review,
            CaseCustodyState.Pending,
            workItemId,
            false,
            vehicleLookupWorkId);
}
