using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.CaseMatching;

public sealed class AutomaticMailCaseAssociationTests
{
    private static readonly Guid ReceiptId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseB = Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static TheoryData<string?, Guid[], string?, Guid[], Guid?> Decisions => new()
    {
        { "AB12CDE", [CaseA], null, [], CaseA },
        { null, [], "thread-1", [CaseA], CaseA },
        { "AB12CDE", [CaseA], "thread-1", [CaseA], CaseA },
        { "AB12CDE", [], null, [], null },
        { "AB12CDE", [CaseA, CaseB], null, [], null },
        { null, [], "thread-1", [CaseA, CaseB], null },
        { "AB12CDE", [CaseA], "thread-1", [CaseB], null }
    };

    [Theory]
    [MemberData(nameof(Decisions))]
    public async Task AssociatesOnlyOneAgreeingCurrentCandidate(
        string? registration,
        Guid[] registrationCases,
        string? thread,
        Guid[] threadCases,
        Guid? expectedCaseId)
    {
        var evidence = new AutomaticMailCaseAssociationEvidence(
            ReceiptId,
            3,
            registration,
            registrationCases,
            "mailbox-1",
            thread,
            threadCases);
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(
            new EvidenceQueries(evidence),
            store,
            new FixedTimeProvider());

        var outcome = await sut.ExecuteAsync(ReceiptId);

        Assert.Equal(expectedCaseId is null ? null : AutomaticCaseAssociationOutcome.Associated, outcome);
        Assert.Equal(expectedCaseId, store.Request?.CaseId);
        if (expectedCaseId is not null)
        {
            Assert.Equal(AssociateRetainedMailWithCase.PolicyKey, store.Request!.MatchPolicyKey);
            Assert.Equal($"mail-case-association:{ReceiptId:N}", store.Request.OperationKey);
            Assert.Equal(evidence.Fingerprint, store.Request.ExpectedEvidenceFingerprint);
        }
    }

    private sealed class EvidenceQueries(AutomaticMailCaseAssociationEvidence evidence)
        : IAutomaticMailCaseAssociationEvidenceQueries
    {
        public Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => Task.FromResult<AutomaticMailCaseAssociationEvidence?>(evidence);
    }

    private sealed class RecordingStore : IAutomaticCaseAssociationStore
    {
        public AutomaticCaseAssociationRequest? Request { get; private set; }

        public Task<AutomaticCaseAssociationOutcome> AssociateFromMatchAsync(
            AutomaticCaseAssociationRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(AutomaticCaseAssociationOutcome.Associated);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 8, 20, 9, 0, 0, TimeSpan.Zero);
    }
}
