using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Intake.CaseMatching;

public sealed class EvaluateIntakeCaseMatchTests
{
    private static readonly Guid CaseA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CaseB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CaseC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    /// <summary>
    /// H10: the DECLARED path normalizes the Principal's raw values through the
    /// provider's own DeriveIndexKeys — the same grammar the write side indexes
    /// cases with — before the shared eliminator sees them. No second grammar
    /// and no second decision procedure exist.
    /// </summary>
    [Fact]
    public async Task DeclaredIdentityUsesTheProvidersExistingNormalizationAndEliminator()
    {
        var derived = new CaseMatchIndexKeys("12345/1", "AB12CDE", "SMITH", "J", null);
        var policy = new StubPolicy(Keys()) { DerivedKeys = derived };
        var sut = new EvaluateIntakeCaseMatch(
            [policy],
            new StubQueries([Candidate(CaseA, claim: "12345/1", vrm: "AB12CDE")]));

        var result = await sut.ExecuteDeclaredAsync(
            "QDOS",
            new("AB/12345/1", "AB12 CDE", "Jane Smith", null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseA, result.MatchedCaseId);
        Assert.Equal(derived.DurableClaimToken, result.Keys.DurableClaimToken);
        Assert.Equal(derived.NormalizedVrm, result.Keys.NormalizedVrm);
        Assert.Equal("qdos_case_match", result.PolicyKey);

        // The RAW declared values are what the policy is handed; the widening
        // happens after normalization, never before it.
        Assert.Equal(
            new CaseMatchSourceData("AB/12345/1", "AB12 CDE", "Jane Smith", null),
            policy.ObservedSourceData);
    }

    [Fact]
    public async Task DeclaredIdentityMatchingSeveralCasesIsAmbiguousNotUnique()
    {
        var policy = new StubPolicy(Keys())
        {
            DerivedKeys = new(null, "AB12CDE", null, null, null)
        };
        var sut = new EvaluateIntakeCaseMatch(
            [policy],
            new StubQueries(
            [
                Candidate(CaseA, vrm: "AB12CDE"),
                Candidate(CaseB, vrm: "AB12CDE")
            ]));

        var result = await sut.ExecuteDeclaredAsync(
            "QDOS",
            new(null, "AB12 CDE", null, null),
            CancellationToken.None);

        Assert.Equal(CaseMatchOutcome.Ambiguous, result!.Outcome);
        Assert.Null(result.MatchedCaseId);
    }

    [Fact]
    public async Task DeclaredIdentityWithNoKeysIsNoKeysAndNeverBlocksCreation()
    {
        var sut = new EvaluateIntakeCaseMatch(
            [new StubPolicy(Keys())],
            new StubQueries([Candidate(CaseA, vrm: "AB12CDE")]));

        var result = await sut.ExecuteDeclaredAsync(
            "QDOS",
            new(null, null, null, null),
            CancellationToken.None);

        Assert.Equal(CaseMatchOutcome.NoKeys, result!.Outcome);
    }

    [Fact]
    public async Task APrincipalWithNoCaseMatchPolicyProducesNoDeclaredDecision()
    {
        var sut = new EvaluateIntakeCaseMatch(
            [new StubPolicy(Keys()) { Provider = "PCH" }],
            new StubQueries([]));

        Assert.Null(await sut.ExecuteDeclaredAsync(
            "QDOS",
            new("AB/12345/1", null, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task NonAcceptedRouteProducesNoDecision()
    {
        var result = await Execute(
            Keys(claim: "12345/1"),
            [],
            route: Route(MailRouteDisposition.NoMatch, null));

        Assert.Null(result);
    }

    [Fact]
    public async Task ProviderWithoutAnAcceptedPolicyProducesNoDecision()
    {
        var sut = new EvaluateIntakeCaseMatch(
            [new StubPolicy(Keys(claim: "12345/1")) { Provider = "PCH" }],
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
            Keys(claim: "12345/1"),
            [Candidate(CaseA, claim: "12345/1")]);

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
            Keys(claim: "12345/1", vrm: "CD34EFG"),
            [
                Candidate(CaseA, claim: "12345/1"),
                Candidate(CaseB, vrm: "CD34EFG")
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
            Keys(claim: "12345/1", vrm: "AB12CDE"),
            [Candidate(CaseA, claim: "12345/1", vrm: "XY65ZZZ")]);

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
    public async Task NameContradictionEliminatesAVrmHitCandidate()
    {
        var result = await Execute(
            Keys(surname: "SMITH", initial: "J", vrm: "AB12CDE"),
            [Candidate(CaseA, surname: "JONES", initial: "B", vrm: "AB12CDE")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Null(result.MatchedCaseId);
        var evaluation = Assert.Single(result.Candidates);
        Assert.Contains(EvaluateIntakeCaseMatch.VehicleRegistrationKey, evaluation.HitKeys);
        Assert.Contains(
            evaluation.Eliminations,
            reason => reason.Contains("claimant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NameContradictionEliminatesAClaimHitCandidate()
    {
        var result = await Execute(
            Keys(claim: "12345/1", surname: "SMITH", initial: "J"),
            [Candidate(CaseA, claim: "12345/1", surname: "JONES", initial: "B")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.NotEmpty(Assert.Single(result.Candidates).Eliminations);
    }

    [Fact]
    public async Task SameSurnameWithADifferentInitialIsARecordedContradiction()
    {
        var result = await Execute(
            Keys(surname: "KHAN", initial: "S"),
            [Candidate(CaseA, surname: "KHAN", initial: "A")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        var evaluation = Assert.Single(result.Candidates);
        Assert.Empty(evaluation.HitKeys);
        Assert.NotEmpty(evaluation.Eliminations);
    }

    [Fact]
    public async Task PartialNamePairNeverEliminates()
    {
        var result = await Execute(
            Keys(claim: "12345/1", surname: "SMITH"),
            [Candidate(CaseA, claim: "12345/1", surname: "JONES", initial: "B")]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseA, result.MatchedCaseId);
    }

    [Fact]
    public async Task CreatedInErrorSurvivorRedirectsToItsLinkedReplacementEvaluatedOnItsOwnKeys()
    {
        var result = await Execute(
            Keys(claim: "12345/1"),
            [
                Candidate(
                    CaseA,
                    claim: "12345/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseB)
            ],
            byCaseId: [Candidate(CaseB, claim: "12345/1")]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseB, result.MatchedCaseId);
        Assert.Equal(CaseA, result.RedirectedFromCaseId);
    }

    [Fact]
    public async Task ReplacementContradictedByTheMessageIsEliminatedNotInherited()
    {
        var result = await Execute(
            Keys(claim: "12345/1", date: new DateOnly(2026, 6, 18)),
            [
                Candidate(
                    CaseA,
                    claim: "12345/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseB)
            ],
            byCaseId: [Candidate(CaseB, claim: "12345/1", date: new DateOnly(2025, 1, 2))]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Null(result.MatchedCaseId);
        Assert.NotEmpty(Assert.Single(result.Candidates).Eliminations);
    }

    [Fact]
    public async Task ReplacementSharingNoIdentityWithTheMessageFailsClosed()
    {
        var result = await Execute(
            Keys(claim: "12345/1"),
            [
                Candidate(
                    CaseA,
                    claim: "12345/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseB)
            ],
            byCaseId: [Candidate(CaseB, claim: "99999/9")]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Null(result.MatchedCaseId);
    }

    [Fact]
    public async Task ReplacementWithoutAnIndexIdentityFailsClosed()
    {
        var result = await Execute(
            Keys(claim: "12345/1"),
            [
                Candidate(
                    CaseA,
                    claim: "12345/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseB)
            ],
            byCaseId: []);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Contains(
            Assert.Single(result.Candidates).Eliminations,
            reason => reason.Contains("no match-index identity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChainedCreatedInErrorReplacementsResolveToTheLiveCase()
    {
        var result = await Execute(
            Keys(claim: "12345/1"),
            [
                Candidate(
                    CaseA,
                    claim: "12345/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseB)
            ],
            byCaseId:
            [
                Candidate(
                    CaseB,
                    claim: "12345/1",
                    state: CaseLifecycleState.CreatedInError,
                    replacement: CaseC),
                Candidate(CaseC, claim: "12345/1")
            ]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseC, result.MatchedCaseId);
        Assert.Equal(CaseA, result.RedirectedFromCaseId);
    }

    [Fact]
    public async Task CreatedInErrorWithoutAReplacementIsEliminatedWithAReason()
    {
        var result = await Execute(
            Keys(claim: "12345/1"),
            [Candidate(CaseA, claim: "12345/1", state: CaseLifecycleState.CreatedInError)]);

        Assert.Equal(CaseMatchOutcome.NoMatch, result!.Outcome);
        Assert.Contains(
            Assert.Single(result.Candidates).Eliminations,
            reason => reason.Contains("Created in error", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedirectTargetAlreadyACandidateDeduplicatesToOneSurvivor()
    {
        var result = await Execute(
            Keys(claim: "12345/1", vrm: "AB12CDE"),
            [
                Candidate(
                    CaseA,
                    claim: "12345/1",
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
            Keys(claim: "12345/1"),
            [Candidate(CaseA, claim: "12345/1", state: state)]);

        Assert.Equal(CaseMatchOutcome.UniqueMatch, result!.Outcome);
        Assert.Equal(CaseA, result.MatchedCaseId);
    }

    private static Task<CaseMatchEvaluationResult?> Execute(
        CaseMatchKeys keys,
        IReadOnlyList<CaseMatchCandidate> candidates,
        MailRouteEvaluationResult? route = null,
        IReadOnlyList<CaseMatchCandidate>? byCaseId = null)
    {
        var sut = new EvaluateIntakeCaseMatch(
            [new StubPolicy(keys)],
            new StubQueries(candidates, byCaseId));
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
        public CaseMatchIndexKeys DerivedKeys { get; init; } = new(null, null, null, null, null);
        public CaseMatchSourceData? ObservedSourceData { get; private set; }
        public CaseMatchKeys ExtractMatchKeys(IntakeSourceReadResult readResult) => keys;
        public CaseMatchIndexKeys DeriveIndexKeys(CaseMatchSourceData caseData)
        {
            ObservedSourceData = caseData;
            return DerivedKeys;
        }
    }

    private sealed class StubQueries(
        IReadOnlyList<CaseMatchCandidate> candidates,
        IReadOnlyList<CaseMatchCandidate>? byCaseId = null)
        : ICaseMatchCandidateQueries
    {
        public Task<IReadOnlyList<CaseMatchCandidate>> FindByAnyKeyAsync(
            string workProviderCode,
            CaseMatchKeys keys,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);

        public Task<CaseMatchCandidate?> FindByCaseIdAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                (byCaseId ?? candidates).FirstOrDefault(item => item.CaseId == caseId));
    }
}
