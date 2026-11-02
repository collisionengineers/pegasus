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

    /// <summary>
    /// Creating a Case directly is casework (ADR-0064): the Automation Actor
    /// creates one in its own name; a system worker or Principal cannot.
    /// </summary>
    [Fact]
    public async Task TheAutomationActorCreatesACaseAndOtherActorsCannot()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => useCase.ExecuteAsync(
            Request("manual-create-system") with { Actor = ActionActor.SystemWorker("worker") },
            CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => useCase.ExecuteAsync(
            Request("manual-create-principal") with { Actor = ActionActor.Principal(Guid.NewGuid()) },
            CancellationToken.None));
        Assert.Null(store.Request);

        await useCase.ExecuteAsync(
            Request("mcp:manual-create-automation") with { Actor = ActionActor.Automation("grant-1") },
            CancellationToken.None);

        Assert.Equal(ActorKind.Automation, store.Request!.Actor.Kind);
        Assert.Equal("mcp:manual-create-automation", store.Request.OperationKey);
    }

    /// <summary>
    /// An Audit may be created directly (operator, 30 October 2026, R595): it
    /// takes the same identity-critical facts as an Inspection, and its
    /// original report is a Case requirement rather than a creation refusal.
    /// </summary>
    [Fact]
    public async Task AStandaloneAuditMayBeCreatedDirectly()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        await useCase.ExecuteAsync(new(
            Staff,
            "manual-create-2",
            "CE",
            CaseType.Audit,
            new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "AB12CDE")),
            CancellationToken.None);

        Assert.Equal(CaseType.Audit, store.Request!.CaseType);
    }

    /// <summary>
    /// A Triage Case asks only for its Principal and the vehicle registration
    /// (decision R): nothing else is required.
    /// </summary>
    [Fact]
    public async Task ATriageCaseNeedsOnlyThePrincipalAndTheRegistration()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        await useCase.ExecuteAsync(new(
            Staff,
            "manual-triage-1",
            " ce ",
            CaseType.Triage,
            new(VehicleRegistration: " ab 12 cde ")),
            CancellationToken.None);

        Assert.NotNull(store.Request);
        Assert.Equal(CaseType.Triage, store.Request!.CaseType);
        Assert.Equal("CE", store.Request.PrincipalCode);
        Assert.Equal("AB12CDE", store.Request.Data.VehicleRegistration);
        Assert.Null(store.Request.Data.ClaimantName);
        Assert.Null(store.Request.Data.ClaimNumber);
    }

    [Fact]
    public async Task ATriageCaseWithoutARegistrationIsRefused()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(
            Staff,
            "manual-triage-2",
            "CE",
            CaseType.Triage,
            new(ClaimantName: "Jane Doe", ClaimNumber: "C-1")),
            CancellationToken.None));

        Assert.Equal("A manual case needs Vehicle registration.", exception.Message);
        Assert.Null(store.Request);
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

    /// <summary>
    /// A one-off Case for a private customer names no Principal and needs the
    /// claimant's name and the registration; the claim number plays no part
    /// (operator, 31 October and 1 November 2026; R574, R577, R578).
    /// </summary>
    [Fact]
    public async Task AOneOffNeedsOnlyTheClaimantAndTheRegistration()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        await useCase.ExecuteAsync(new(
            Staff,
            "manual-private-1",
            null,
            CaseType.InspectionAndAudit,
            new(ClaimantName: " Pat  Private ", VehicleRegistration: " ab 12 cde ", ClaimantAddress: "1 Private Lane\nExampleton")),
            CancellationToken.None);

        Assert.NotNull(store.Request);
        Assert.True(store.Request!.IsPrivateCustomer);
        Assert.Null(store.Request.PrincipalCode);
        Assert.Equal("Pat Private", store.Request.Data.ClaimantName);
        Assert.Equal("AB12CDE", store.Request.Data.VehicleRegistration);
        Assert.Null(store.Request.Data.ClaimNumber);
        Assert.Equal("1 Private Lane\nExampleton", store.Request.Data.ClaimantAddress);
    }

    [Fact]
    public async Task AOneOffWithoutAClaimantIsRefused()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(
            Staff,
            "manual-private-2",
            null,
            CaseType.Inspection,
            new(ClaimNumber: "C-1", VehicleRegistration: "AB12CDE")),
            CancellationToken.None));

        Assert.Equal("A manual case needs Claimant name.", exception.Message);
        Assert.Null(store.Request);
    }

    /// <summary>A one-off is an Inspection or an Inspection + Audit, never a Triage (R577).</summary>
    [Fact]
    public async Task AOneOffTriageIsRefused()
    {
        var store = new RecordingStore();
        var useCase = new CreateManualCase(store, new CommittedWorkPublisherDouble());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(
            Staff,
            "manual-private-3",
            null,
            CaseType.Triage,
            new(VehicleRegistration: "AB12CDE")),
            CancellationToken.None));

        Assert.Equal("The case type is invalid.", exception.Message);
        Assert.Null(store.Request);
    }

    [Theory]
    [InlineData(CaseType.Inspection, false, new[] { "Claimant name", "Claim number", "Vehicle registration" })]
    [InlineData(CaseType.Inspection, true, new[] { "Claimant name", "Vehicle registration" })]
    [InlineData(CaseType.Triage, false, new[] { "Vehicle registration" })]
    public void WhatACaseCreatedDirectlyNeedsDependsOnItsKind(CaseType caseType, bool privateCustomer, string[] expected)
    {
        var empty = new Pegasus.Core.Intake.InstructionDraft(
            null, null, null, null, null, null, null, null, null, null, null);

        Assert.Equal(expected, ManualCaseCreationPolicy.MissingFieldNames(caseType, privateCustomer, empty));
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
