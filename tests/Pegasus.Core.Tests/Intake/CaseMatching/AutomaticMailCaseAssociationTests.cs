using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.CaseMatching;

public sealed class AutomaticMailCaseAssociationTests
{
    private static readonly Guid ReceiptId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseA = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseB = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid CaseC = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly DateOnly IncidentDate = new(2031, 4, 1);
    private static readonly DateOnly OtherIncidentDate = new(2031, 5, 1);

    /// <summary>
    /// Every signal contributes its whole candidate set and the sets are
    /// intersected (D16). A registration several Cases share is narrowed by the
    /// message's incident date first (D15); when that leaves none, the
    /// registration contributes nothing and the remaining signals decide.
    /// </summary>
    public static TheoryData<
        string,
        string?,
        Guid[],
        DateOnly?,
        DateOnly?[],
        string?,
        Guid[],
        Guid[],
        Guid[],
        Guid?> Decisions => new()
    {
        { "thread-only", null, [], null, [], "thread-1", [CaseA], [], [], CaseA },
        { "reply-only", null, [], null, [], null, [], [CaseA], [], CaseA },
        { "claim-only", null, [], null, [], null, [], [], [CaseA], CaseA },
        { "registration-only", "AB12CDE", [CaseA], null, [null], null, [], [], [], CaseA },
        {
            "thread-and-reply-agree",
            null, [], null, [], "thread-1", [CaseA], [CaseA], [], CaseA
        },
        {
            "thread-and-claim-agree",
            null, [], null, [], "thread-1", [CaseA], [], [CaseA], CaseA
        },
        {
            "all-four-agree",
            "AB12CDE", [CaseA], IncidentDate, [IncidentDate], "thread-1", [CaseA], [CaseA], [CaseA], CaseA
        },
        {
            "thread-disagrees-with-reply",
            null, [], null, [], "thread-1", [CaseA], [CaseB], [], null
        },
        {
            "thread-disagrees-with-registration",
            "AB12CDE", [CaseA], null, [null], "thread-1", [CaseB], [], [], null
        },
        {
            "two-threads-abstain",
            null, [], null, [], "thread-1", [CaseA, CaseB], [], [], null
        },
        {
            "broad-registration-narrowed-by-agreeing-thread",
            "AB12CDE", [CaseA, CaseB], null, [null, null], "thread-1", [CaseA], [], [], CaseA
        },
        {
            "registration-narrowed-to-one",
            "AB12CDE", [CaseA, CaseB], IncidentDate, [IncidentDate, OtherIncidentDate], null, [], [], [], CaseA
        },
        {
            "registration-narrowed-to-none-contributes-nothing",
            "AB12CDE", [CaseA, CaseB], new DateOnly(2031, 6, 1), [IncidentDate, OtherIncidentDate], null, [], [], [], null
        },
        {
            "registration-narrowed-to-none-does-not-veto-the-thread",
            "AB12CDE", [CaseA, CaseB], new DateOnly(2031, 6, 1), [IncidentDate, OtherIncidentDate],
            "thread-1", [CaseA], [], [], CaseA
        },
        {
            "registration-dates-still-tied",
            "AB12CDE", [CaseA, CaseB], IncidentDate, [IncidentDate, IncidentDate], null, [], [], [], null
        },
        {
            "registration-dates-still-tied-narrowed-by-thread",
            "AB12CDE", [CaseA, CaseB], IncidentDate, [IncidentDate, IncidentDate],
            "thread-1", [CaseA], [], [], CaseA
        },
        {
            "registration-narrowed-to-one-disagrees-with-thread",
            "AB12CDE", [CaseA, CaseB], IncidentDate, [IncidentDate, OtherIncidentDate],
            "thread-1", [CaseB], [], [], null
        },
        { "no-signal-abstains", null, [], null, [], null, [], [], [], null }
    };

    [Theory]
    [MemberData(nameof(Decisions))]
    public async Task AssociatesTheOneCaseEveryNonEmptySignalAgreesOn(
        string row,
        string? registration,
        Guid[] registrationCases,
        DateOnly? incidentDate,
        DateOnly?[] registrationIncidentDates,
        string? thread,
        Guid[] threadCases,
        Guid[] replyCases,
        Guid[] claimCases,
        Guid? expectedCaseId)
    {
        var evidence = Evidence(
            registration,
            registrationCases,
            incidentDate,
            registrationIncidentDates,
            thread,
            threadCases,
            replyCases,
            claimCases);
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(
            new EvidenceQueries(evidence),
            store,
            new FixedTimeProvider());

        var outcome = await sut.ExecuteAsync(ReceiptId);

        Assert.Equal(expectedCaseId is null ? null : AutomaticCaseAssociationOutcome.Associated, outcome);
        if (expectedCaseId is null)
        {
            Assert.True(store.Request is null, $"{row}: an association was requested.");
            return;
        }

        Assert.True(store.Request is not null, $"{row}: no association was requested.");
        Assert.Equal(expectedCaseId, store.Request!.CaseId);
        Assert.Equal(AssociateRetainedMailWithCase.PolicyKey, store.Request.MatchPolicyKey);
        Assert.Equal(2, store.Request.MatchPolicyVersion);
        Assert.Equal($"mail-case-association:{ReceiptId:N}", store.Request.OperationKey);
        Assert.Equal(evidence.Fingerprint, store.Request.ExpectedEvidenceFingerprint);
    }

    /// <summary>
    /// The reason is part of the write's request hash, so it names the signals
    /// that agreed, in one fixed order, and nothing else varies it.
    /// </summary>
    [Fact]
    public async Task NamesEveryAgreeingSignalInTheRecordedReason()
    {
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(
            new EvidenceQueries(Evidence(
                "AB12CDE",
                [CaseA],
                IncidentDate,
                [IncidentDate],
                "thread-1",
                [CaseA],
                [CaseA],
                [CaseA])),
            store,
            new FixedTimeProvider());

        await sut.ExecuteAsync(ReceiptId);

        Assert.Equal(
            "Automatic association from conversation thread, reply headers, claim reference and vehicle registration.",
            store.Request!.Reason);
    }

    [Fact]
    public async Task NamesASingleSignalWithoutAList()
    {
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(
            new EvidenceQueries(Evidence(
                null, [], null, [], null, [], [CaseA], [])),
            store,
            new FixedTimeProvider());

        await sut.ExecuteAsync(ReceiptId);

        Assert.Equal("Automatic association from reply headers.", store.Request!.Reason);
    }

    /// <summary>
    /// The plate the photographs inside the message's PDF read is its
    /// registration when it names none (operator, 28 October 2026, R474): the
    /// evidence is read with it, and the write re-reads the evidence with it.
    /// </summary>
    [Fact]
    public async Task APhotographReadIsTheRegistrationSignal()
    {
        var queries = new EvidenceQueries(Evidence(
            "SH65HTL", [CaseA], null, [null], null, [], [], []));
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(queries, store, new FixedTimeProvider());

        var outcome = await sut.ExecuteAsync(ReceiptId, photographRegistration: "SH65HTL");

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, outcome);
        Assert.Equal("SH65HTL", queries.PhotographRegistration);
        Assert.Equal(CaseA, store.Request!.CaseId);
        Assert.Equal("SH65HTL", store.Request.PhotographRegistration);
        Assert.Equal("Automatic association from vehicle registration.", store.Request.Reason);
    }

    // #1470: a Case the recorded principal_case_match eliminated, or an
    // Ambiguous decision, never becomes the mail association target. The
    // intersection runs first, so an eliminated Case still counts.
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
        var queries = new EvidenceQueries(Evidence(
            registrationCases.Length == 0 ? null : "AB12CDE",
            registrationCases,
            null,
            registrationCases.Select(_ => (DateOnly?)null).ToArray(),
            threadCases.Length == 0 ? null : "thread-1",
            threadCases,
            [],
            []));
        var store = new RecordingStore();
        var sut = new AssociateRetainedMailWithCase(queries, store, new FixedTimeProvider());

        var outcome = await sut.ExecuteAsync(ReceiptId, decision);

        Assert.Equal(expectedCaseId is null ? null : AutomaticCaseAssociationOutcome.Associated, outcome);
        Assert.Equal(expectedCaseId, store.Request?.CaseId);
        var expectedReads = decision?.Outcome == CaseMatchOutcome.Ambiguous ? 0 : 1;
        Assert.True(expectedReads == queries.Calls, $"{row}: {queries.Calls} evidence reads, expected {expectedReads}.");
    }

    private static AutomaticMailCaseAssociationEvidence Evidence(
        string? registration,
        Guid[] registrationCases,
        DateOnly? incidentDate,
        DateOnly?[] registrationIncidentDates,
        string? thread,
        Guid[] threadCases,
        Guid[] replyCases,
        Guid[] claimCases) =>
        new(
            ReceiptId,
            3,
            "mailbox-1",
            thread,
            registration,
            incidentDate,
            threadCases,
            replyCases,
            claimCases,
            registrationCases
                .Select((caseId, index) => new MailAssociationRegistrationCandidate(
                    caseId,
                    index < registrationIncidentDates.Length ? registrationIncidentDates[index] : null))
                .ToArray());

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

        public string? PhotographRegistration { get; private set; }

        public Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
            Guid intakeReceiptId,
            string? photographRegistration,
            CancellationToken cancellationToken)
        {
            Calls++;
            PhotographRegistration = photographRegistration;
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
