using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class ImageIntakeCasePairingTests
{
    private static readonly Guid CaseId = Guid.NewGuid();

    [Fact]
    public async Task PairsEveryUnassociatedIntakeWhoseSingleCandidateIsTheNewCase()
    {
        var first = Summary("AB12CDE-01", "AB12CDE");
        var second = Summary("AB12CDE-02", "AB12CDE");
        var queries = new FakeQueries { Unassociated = [first, second] };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE")]
        };
        var mutationStore = new FakeMutationStore();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Equal(2, mutationStore.AutoLinks.Count);
        Assert.All(mutationStore.AutoLinks, link => Assert.Equal(CaseId, link.CaseId));
        Assert.All(
            mutationStore.AutoLinks,
            link => Assert.Equal(ActorKind.SystemWorker, link.Actor.Kind));
    }

    [Fact]
    public async Task ADifferentOrAmbiguousCandidateSetPairsNothing()
    {
        var queries = new FakeQueries { Unassociated = [Summary("AB12CDE-01", "AB12CDE")] };
        var candidates = new FakeCandidates
        {
            Result =
            [
                new(CaseId, "QDS26001", 0, "AB12CDE"),
                new(Guid.NewGuid(), "QDS26002", 0, "AB12CDE")
            ]
        };
        var mutationStore = new FakeMutationStore();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Empty(mutationStore.AutoLinks);
    }

    [Fact]
    public async Task ANearMissCandidateNeverPairsInTheReverseDirection()
    {
        // The registered identity is immutable here, so the scan-time
        // completion rules cannot apply: a case whose confirmed registration
        // is one character off the intake's registered value stays a staff
        // suggestion.
        var queries = new FakeQueries { Unassociated = [Summary("AB12CDE-01", "AB12CDE")] };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDEF")]
        };
        var mutationStore = new FakeMutationStore();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Empty(mutationStore.AutoLinks);
    }

    [Fact]
    public async Task OneFailedPairingNeverStopsTheOthers()
    {
        var first = Summary("AB12CDE-01", "AB12CDE");
        var second = Summary("AB12CDE-02", "AB12CDE");
        var queries = new FakeQueries { Unassociated = [first, second] };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE")]
        };
        var mutationStore = new FakeMutationStore
        {
            FailFor = first.OriginReceiptId
        };

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        var link = Assert.Single(mutationStore.AutoLinks);
        Assert.Equal(second.OriginReceiptId, link.ReceiptId);
    }

    [Fact]
    public async Task StaffClosedIntakesAreNeverTreatedAsPairingCandidates()
    {
        // A Staff-closed record has no Case association either, so the old
        // `associated: false` filter would have offered it to the pairing
        // scan forever. Filtering on lifecycle state instead must exclude it.
        var closed = Summary("AB12CDE-01", "AB12CDE", state: ImageInitiatedCaseState.StaffClosed);
        var queries = new FakeQueries { Unassociated = [closed] };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE")]
        };
        var mutationStore = new FakeMutationStore();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Empty(mutationStore.AutoLinks);
    }

    [Fact]
    public async Task AnAlreadyLinkedAwaitingIntakeRetriesTheMergeWithoutRelinking()
    {
        // AutoLinkAsync already succeeded on a previous pass (or a manual
        // link happened) but the merge did not commit; this pass must retry
        // only the merge, not attempt to link again.
        var receiptId = Guid.NewGuid();
        var summary = Summary("AB12CDE-01", "AB12CDE", associatedCaseId: CaseId) with
        {
            OriginReceiptId = receiptId
        };
        var detail = new ImageIntakeDetail(
            new ImageIntakeRecord(
                Guid.NewGuid(),
                new ImageIntakeOrigin(
                    receiptId,
                    new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "token"),
                    new string('a', 64),
                    Guid.NewGuid()),
                "AB12CDE",
                "AB12CDE-01"),
            DateTimeOffset.UtcNow,
            CaseId,
            "QDS26001");
        var queries = new FakeQueries
        {
            Unassociated = [summary],
            ByOriginReceipt = { [receiptId] = detail }
        };
        var candidates = new FakeCandidates();
        var mutationStore = new FakeMutationStore();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Empty(mutationStore.AutoLinks);
        var merge = Assert.Single(queries.Merges);
        Assert.Equal(detail.Record.Id, merge.ImageIntakeId);
        Assert.Equal(CaseId, merge.CaseId);
    }

    private static ImageIntakeSummary Summary(
        string reference,
        string registration,
        ImageInitiatedCaseState state = ImageInitiatedCaseState.AwaitingInstruction,
        Guid? associatedCaseId = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        reference,
        registration,
        associatedCaseId,
        null,
        DateTimeOffset.UtcNow,
        state);

    private sealed class FakeQueries : IImageIntakeStore
    {
        public IReadOnlyList<ImageIntakeSummary> Unassociated { get; init; } = [];

        public Dictionary<Guid, ImageIntakeDetail> ByOriginReceipt { get; init; } = [];

        public List<MergeImageInitiatedCaseRequest> Merges { get; } = [];

        public Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ImageIntakeRecord> RegisterAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnsureRegisteredReceiptDecisionAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ImageIntakeRecord> MergeAsync(
            MergeImageInitiatedCaseRequest request,
            CancellationToken cancellationToken)
        {
            Merges.Add(request);
            return Task.FromResult(new ImageIntakeRecord(
                request.ImageIntakeId,
                new ImageIntakeOrigin(
                    Guid.NewGuid(),
                    new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, "token"),
                    new string('a', 64),
                    Guid.NewGuid()),
                "AB12CDE",
                "AB12CDE-01",
                ImageInitiatedCaseState.MergedIntoInstructionCase));
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken)
        {
            Assert.Null(associated);
            return Task.FromResult(Unassociated);
        }

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                ByOriginReceipt.TryGetValue(intakeReceiptId, out var detail) ? detail : null);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
    }

    private sealed class FakeCandidates : IImageIntakeCaseCandidates
    {
        public IReadOnlyList<ImageIntakeCaseCandidate> Result { get; init; } = [];

        public Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) =>
            Task.FromResult(normalizedVehicleRegistration == "AB12CDE" ? Result : []);
    }

    private sealed class FakeMutationStore : IIntakeMutationStore
    {
        public List<AutomaticIntakeLinkRequest> AutoLinks { get; } = [];

        public Guid? FailFor { get; init; }

        public Task<IntakeReceipt> ResolveAsync(
            ResolveIntakeRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleReevaluationAsync(
            ReevaluateIntakeRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task LinkAsync(
            LinkIntakeRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReverseLinkAsync(
            ReverseIntakeLinkRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AutoLinkAsync(
            AutomaticIntakeLinkRequest request,
            DateTimeOffset occurredAtUtc,
            CancellationToken cancellationToken)
        {
            if (request.ReceiptId == FailFor)
            {
                throw new IntakeAssociationConflictException("A staff lease is active.");
            }

            AutoLinks.Add(request);
            return Task.CompletedTask;
        }
    }
}
