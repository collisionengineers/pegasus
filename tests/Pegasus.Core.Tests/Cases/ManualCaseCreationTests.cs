using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tests.Support;

namespace Pegasus.Core.Tests.Cases;

public sealed class ManualCaseCreationTests
{
    private static readonly ActionActor Staff = ActionActor.Staff(
        Guid.Parse("7787b09c-675d-4acd-944a-7c82a4360d33"),
        [StaffRole.Administrator]);

    [Fact]
    public async Task NormalizesStaffEnteredFactsBeforeWriting()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        await useCase.ExecuteAsync(new(
            Staff,
            "manual-create-1",
            " ce ",
            CaseType.Inspection,
            new(ClaimantName: "  Jane   Doe ", ClaimNumber: "  C-1 ", VehicleRegistration: " ab 12 cde ")),
            CancellationToken.None);

        Assert.NotNull(store.Request);
        Assert.Equal("CE", store.Request!.PrincipalCode);
        Assert.Equal("Jane Doe", store.Request.Data.ClaimantName);
        Assert.Equal("C-1", store.Request.Data.ClaimNumber);
        Assert.Equal("AB12CDE", store.Request.Data.VehicleRegistration);
    }

    [Fact]
    public async Task RefusesAStandaloneAuditWithoutRetainedEvidence()
    {
        var useCase = new CreateManualCase(new RecordingStore(), new CommittedWorkPublisherDouble());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(
            Staff,
            "manual-create-2",
            "CE",
            CaseType.Audit,
            new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "AB12CDE")),
            CancellationToken.None));

        Assert.Contains("retained original-report evidence", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The vehicle lookup the creation transaction enqueued is published as
    /// soon as it commits, so DVLA/MOT evidence arrives with the new Case
    /// instead of on the Worker's next reconciliation sweep (FRD-06 D34).
    /// </summary>
    [Fact]
    public async Task PublishesTheVehicleLookupTheCreationTransactionEnqueued()
    {
        var vehicleLookupWorkId = Guid.NewGuid();
        var publisher = new CommittedWorkPublisherDouble();
        var useCase = new CreateManualCase(new RecordingStore(vehicleLookupWorkId), publisher);

        await useCase.ExecuteAsync(Request("manual-create-3"), CancellationToken.None);

        Assert.Equal([vehicleLookupWorkId], publisher.ExternalWorkIds);
    }

    /// <summary>
    /// A Case created without a usable registration, or where lookups are not
    /// composed, enqueues nothing — so there is nothing to publish and no
    /// invented work item id.
    /// </summary>
    [Fact]
    public async Task PublishesNothingWhenNoLookupWasEnqueued()
    {
        var publisher = new CommittedWorkPublisherDouble();
        var useCase = new CreateManualCase(new RecordingStore(), publisher);

        await useCase.ExecuteAsync(Request("manual-create-4"), CancellationToken.None);

        Assert.Empty(publisher.ExternalWorkIds);
    }

    private static CreateManualCaseRequest Request(string operationKey) => new(
        Staff,
        operationKey,
        "CE",
        CaseType.Inspection,
        new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "AB12CDE"));

    private sealed class RecordingStore(Guid? vehicleLookupWorkId = null) : IManualCaseCreationStore
    {
        public CreateManualCaseRequest? Request { get; private set; }

        public Task<ManualCaseCreationOutcome> CreateAsync(
            CreateManualCaseRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new ManualCaseCreationOutcome(
                new CaseIdentity(Guid.NewGuid(), "CE", 2031, 1, "CE31001"),
                vehicleLookupWorkId));
        }
    }
}
