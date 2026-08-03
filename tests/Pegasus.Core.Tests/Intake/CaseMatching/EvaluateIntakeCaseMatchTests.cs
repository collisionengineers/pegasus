using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake.CaseMatching;

public sealed class EvaluateIntakeCaseMatchTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CaseC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task NonAcceptedRouteProducesNoDecision()
    {
        var result = await Execute(
            Keys(claim: "46553/1"),
            [],
            route: Route(MailRouteDisposition.NoMatch, null));

        Assert.Null(result);
    }

    [Fact]
    public async Task ProviderWithoutAnAcceptedPolicyProducesNoDecision()
    {
        var sut = new EvaluateIntakeCaseMatch(
            [new StubPolicy(Keys(claim: "46553/1")) { Provider = "PCH" }],
            new StubQueries([]));

        var result = await sut.ExecuteAsync(
            Readable(),
            Route(MailRouteDisposition.Accepted, new("QDOS", MailRouteKind.DirectProvider, "QDOS")),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task NoExtractableKeyIsRecordedAsNoKeysDistinctFromNoMatch()
    {
        var result = await Execute(Keys(), []);

        Assert.Equal(CaseMatchOutcome.NoKeys, Assert.IsType<CaseMatchEvaluationResult>(result).Outcome);
        Assert.Null(result!.MatchedCaseId);
    }

    [Fact]
    public async Task SingleSurvivingCandidateIsAUniqueMatch()
    {
        var result = await Execute(
            Keys(claim: "46553/1"),
            [Candidate(CaseA, claim: "46553/1")]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseA, result.MatchedCaseId);
        Assert.Contains(
            EvaluateIntakeCaseMatch.ClaimReferenceKey,
            Assert.Single(result.Candidates).HitKeys);
    }

    [Fact]
    public async Task ClaimHitOnOneCaseAndVrmHitOnAnotherIsAmbiguousWithNoInventedWinner()
    {
        var result = await Execute(
            Keys(claim: "46553/1", vrm: "LT17UCU"),
            [
                Candidate(CaseA, claim: "46553/1"),
                Candidate(CaseB, vrm: "LT17UCU")
            ]);

        Assert.Equal(CaseMatchOutcome.Ambiguous, result!.Outcome);
        Assert.Null(result.MatchedCaseId);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public async Task IncidentDateMismatchEliminatesANameOnlyCandidate()
    {
        var result = await Execute(
            Keys(surname: "SMITH", initial: "J", date: new DateOnly(2026, 6, 18)),
            [
                Candidate(
                    CaseA,
                    surname: "SMITH",
                    initial: "J",
                    date: new DateOnly(2025, 1, 2))
            ]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.NotEmpty(Assert.Single(result.Candidates).Eliminations);
    }

    [Fact]
    public async Task IncidentDateEliminationResolvesTwoCandidatesToAUniqueMatch()
    {
        var result = await Execute(
            Keys(surname: "SMITH", initial: "J", vrm: "AB12CDE", date: new DateOnly(2026, 6, 18)),
            [
                Candidate(CaseA, surname: "SMITH", initial: "J", date: new DateOnly(2026, 6, 18)),
                Candidate(CaseB, surname: "SMITH", initial: "J", date: new DateOnly(2025, 1, 2))
            ]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseA, result.MatchedCaseId);
    }

    [Fact]
    public async Task MatchingIncidentDateAloneProvesNothing()
    {
        var result = await Execute(
            Keys(claim: "99999/9", date: new DateOnly(2026, 6, 18)),
            [Candidate(CaseA, claim: "11111/1", date: new DateOnly(2026, 6, 18))]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Null(result.MatchedCaseId);
    }

    [Fact]
    public async Task VrmContradictionEliminatesAClaimHitCandidate()
    {
        var result = await Execute(
            Keys(claim: "46553/1", vrm: "AB12CDE"),
            [Candidate(CaseA, claim: "46553/1", vrm: "XY65ZZZ")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        var evaluation = Assert.Single(result.Candidates);
        Assert.Contains(EvaluateIntakeCaseMatch.ClaimReferenceKey, evaluation.HitKeys);
        Assert.NotEmpty(evaluation.Eliminations);
    }

    [Fact]
    public async Task VrmContradictionEliminatesANameHitCandidate()
    {
        var result = await Execute(
            Keys(surname: "SMITH", initial: "J", vrm: "AB12CDE"),
            [Candidate(CaseA, surname: "SMITH", initial: "J", vrm: "XY65ZZZ")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
    }

    [Fact]
    public async Task SameSurnameWithADifferentInitialIsANonHitNotAContradiction()
    {
        var result = await Execute(
            Keys(surname: "KHAN", initial: "S"),
            [Candidate(CaseA, surname: "KHAN", initial: "A")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public async Task CreatedInErrorSurvivorRedirectsToItsLinkedReplacement()
    {
        var result = await Execute(
            Keys(claim: "46553/1"),
            [
                Candidate(
                    CaseA,
                    claim: "46553/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseB)
            ]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseB, result.MatchedCaseId);
        Assert.Equal(CaseA, result.RedirectedFromCaseId);
    }

    [Fact]
    public async Task CreatedInErrorWithoutAReplacementIsEliminatedWithAReason()
    {
        var result = await Execute(
            Keys(claim: "46553/1"),
            [Candidate(CaseA, claim: "46553/1", state: CaseLifecycleState.CreatedInError)]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Contains(
            Assert.Single(result.Candidates).Eliminations,
            reason => reason.Contains("Created in error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedirectTargetAlreadyACandidateDeduplicatesToOneSurvivor()
    {
        var result = await Execute(
            Keys(claim: "46553/1", vrm: "AB12CDE"),
            [
                Candidate(
                    CaseA,
                    claim: "46553/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseC),
                Candidate(CaseC, vrm: "AB12CDE")
            ]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseC, result.MatchedCaseId);
        Assert.Single(result.Candidates);
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.PostReport)]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    public async Task EveryLifecycleStateExceptCreatedInErrorRemainsEligible(
        CaseLifecycleState state)
    {
        var result = await Execute(
            Keys(claim: "46553/1"),
            [Candidate(CaseA, claim: "46553/1", state: state)]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseA, result.MatchedCaseId);
    }

    private static Task<CaseMatchEvaluationResult?> Execute(
        CaseMatchKeys keys,
        IReadOnlyList<CaseMatchCandidate> candidates,
        MailRouteEvaluationResult? route = null)
    {
        var sut = new EvaluateIntakeCaseMatch(
            [new StubPolicy(keys)],
            new StubQueries(candidates));
        return sut.ExecuteAsync(
            Readable(),
            route ?? Route(
                MailRouteDisposition.Accepted,
                new("QDOS", MailRouteKind.DirectProvider, "QDOS")),
            CancellationToken.None);
    }

    private static CaseMatchKeys Keys(
        string? claim = null,
        string? vrm = null,
        string? surname = null,
        string? initial = null,
        DateOnly? date = null) =>
        new(claim, vrm, surname, initial, date);

    private static CaseMatchCandidate Candidate(
        Guid caseId,
        string? claim = null,
        string? vrm = null,
        string? surname = null,
        string? initial = null,
        DateOnly? date = null,
        CaseLifecycleState state = CaseLifecycleState.Review,
        Guid? replacement = null) =>
        new(caseId, "QDOS", claim, vrm, surname, initial, date, state, replacement);

    private static MailRouteEvaluationResult Route(
        MailRouteDisposition disposition,
        MailRouteSelection? selection) =>
        new(
            disposition,
            selection,
            [],
            "test route",
            "qdos_mail_route",
            3,
            [],
            [],
            null);

    private static IntakeSourceReadResult Readable() =>
        new(IntakeSourceReadStatus.Readable, [], [], [], false);

    private sealed class StubPolicy(CaseMatchKeys keys) : IProviderCaseMatchPolicy
    {
        public string Provider { get; init; } = "QDOS";
        public string WorkProviderCode => Provider;
        public string PolicyKey => "qdos_case_match";
        public int PolicyVersion => 1;
        public CaseMatchKeys ExtractMatchKeys(IntakeSourceReadResult readResult) => keys;
        public CaseMatchIndexKeys DeriveIndexKeys(CaseMatchSourceData caseData) =>
            new(null, null, null, null, null);
    }

    private sealed class StubQueries(IReadOnlyList<CaseMatchCandidate> candidates)
        : ICaseMatchCandidateQueries
    {
        public Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
            string workProviderCode,
            CaseMatchKeys keys,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }
}
