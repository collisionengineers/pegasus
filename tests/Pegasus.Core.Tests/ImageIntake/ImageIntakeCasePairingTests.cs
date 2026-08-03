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

    private static ImageIntakeSummary Summary(string reference, string registration) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        reference,
        registration,
        null,
        null,
        DateTimeOffset.UtcNow);

    private sealed class FakeQueries : IImageIntakeQueries
    {
        public IReadOnlyList<ImageIntakeSummary> Unassociated { get; init; } = [];

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken)
        {
            Assert.False(associated ?? true, "Pairing must query unassociated intakes only.");
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
            Task.FromResult<ImageIntakeDetail?>(null);

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
