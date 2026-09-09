using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

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
        var useCase = new CreateManualCase(store);

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
        var useCase = new CreateManualCase(new RecordingStore());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new(
            Staff,
            "manual-create-2",
            "CE",
            CaseType.Audit,
            new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "AB12CDE")),
            CancellationToken.None));

        Assert.Contains("retained original-report evidence", exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingStore : IManualCaseCreationStore
    {
        public CreateManualCaseRequest? Request { get; private set; }

        public Task<CaseIdentity> CreateAsync(
            CreateManualCaseRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new CaseIdentity(Guid.NewGuid(), "CE", 2031, 1, "CE31001"));
        }
    }
}
