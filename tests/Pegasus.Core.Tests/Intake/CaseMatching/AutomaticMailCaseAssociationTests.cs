using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.CaseMatching;

public sealed class AutomaticMailCaseAssociationTests
{
    private static readonly Guid ReceiptId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseB = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid CaseC = Guid.Parse("20000000-0000-0000-0000-000000000003");

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

    // #1470: a Case the recorded principal_case_match eliminated, or an
    // Ambiguous decision, never becomes the mail association target. The
    // uniqueness rule runs first, so an eliminated Case still counts.
    public static TheoryData<string, Guid[], Guid[], CaseMatchEvaluationResult?, Guid?> RecordedMatches => new()
    {
        { "eliminated-reg", [CaseA], [], Recorded(CaseMatchOutcome.NoMatch, Eliminated(CaseA)), null },
        { "eliminated-thread", [], [CaseA], Recorded(CaseMatchOutcome.NoMatch, Eliminated(CaseA)), null },
        {
            "redirected-from",
            [CaseA],
            [],
            Recorded(CaseMatchOutcome.NoMatch, Eliminated(CaseC) with { RedirectedFromCaseId = CaseA }),
            null
        },
        { "eliminated-plus-other", [CaseA, CaseB], [], Recorded(CaseMatchOutcome.NoMatch, Eliminated(CaseA)), null },
        { "ambiguous", [CaseA], [], Recorded(CaseMatchOutcome.Ambiguous, Survived(CaseA), Survived(CaseB)), null },
        { "survivor", [CaseA], [], Recorded(CaseMatchOutcome.UniqueMatch, Survived(CaseA)), CaseA },
        { "unrelated-elimination", [CaseA], [], Recorded(CaseMatchOutcome.NoMatch, Eliminated(CaseC)), CaseA },
        { "no-decision", [CaseA], [], null, CaseA }
    };

    [Theory]
    [MemberData(nameof(RecordedMatches))]
    public async Task NeverLinksACaseTheRecordedMatchRuledOut(
        string row,
        Guid[] registrationCases,
        Guid[] threadCases,
        CaseMatchEvaluationResult? decision,
        Guid? expectedCaseId)
    {
        var queries = new EvidenceQueries(new(
            ReceiptId,
            3,
            registrationCases.Length == 0 ? null : "AB12CDE",
            registrationCases,
            "mailbox-1",
            threadCases.Length == 0 ? null : "thread-1",
            threadCases));
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(queries, store, new FixedTimeProvider());

        var outcome = await sut.ExecuteAsync(ReceiptId, decision);

        Assert.Equal(expectedCaseId is null ? null : AutomaticCaseAssociationOutcome.Associated, outcome);
        Assert.Equal(expectedCaseId, store.Request?.CaseId);
        var expectedReads = decision?.Outcome == CaseMatchOutcome.Ambiguous ? 0 : 1;
        Assert.True(expectedReads == queries.Calls, $"{row}: {queries.Calls} evidence reads, expected {expectedReads}.");
    }

    private static CaseMatchCandidateEvaluation Eliminated(Guid caseId) => new(
        caseId,
        ["vehicle-registration"],
        ["The message claim reference '49569/1' contradicts the case's '39569/1'."]);

    private static CaseMatchCandidateEvaluation Survived(Guid caseId) => new(
        caseId,
        ["vehicle-registration"],
        []);

    private static CaseMatchEvaluationResult Recorded(
        CaseMatchOutcome outcome,
        params CaseMatchCandidateEvaluation[] candidates) => new(
            outcome,
            outcome == CaseMatchOutcome.UniqueMatch ? candidates[0].CaseId : null,
            null,
            new(null, "AB12CDE", null, null, null),
            candidates,
            "Recorded for the test.",
            PrincipalCaseMatchPolicy.Key,
            PrincipalCaseMatchPolicy.Version);

    private sealed class EvidenceQueries(AutomaticMailCaseAssociationEvidence evidence)
        : IAutomaticMailCaseAssociationEvidenceQueries
    {
        public int Calls { get; private set; }

        public Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<AutomaticMailCaseAssociationEvidence?>(evidence);
        }
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
