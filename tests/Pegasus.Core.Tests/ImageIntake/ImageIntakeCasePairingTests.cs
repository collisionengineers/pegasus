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

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
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

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
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

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
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

        var result = await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        var link = Assert.Single(mutationStore.AutoLinks);
        Assert.Equal(second.OriginReceiptId, link.ReceiptId);
        Assert.Equal(new ImageIntakePairingResult(2, 1, 1, nameof(IntakeAssociationConflictException)), result);
    }

    [Theory]
    [InlineData(1, true, true)]
    [InlineData(2, true, false)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    public void RegisteredSelectionPreservesExactIdentityPrincipalAndPersistedGroupRule(
        int expectedMembers, bool samePrincipal, bool selected)
    {
        var principalId = Guid.NewGuid();
        ImageIntakeCaseCandidate[] candidates =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", principalId),
            new(Guid.NewGuid(), "QDS26002", 0, "AB12CDEF", principalId)
        ];
        var result = ImageIntakeCasePairing.SelectRegisteredTarget(candidates,
            "AB12CDE", samePrincipal ? principalId : Guid.NewGuid(), expectedMembers);
        Assert.Equal(selected, result is not null);
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

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
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
        var workItemId = Guid.NewGuid();
        queries.PendingExternalWorkId = workItemId;
        var publisher = new CommittedWorkPublisherDouble();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, publisher, queries)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Empty(mutationStore.AutoLinks);
        var merge = Assert.Single(queries.Merges);
        Assert.Equal(detail.Record.Id, merge.ImageIntakeId);
        Assert.Equal(CaseId, merge.CaseId);
        Assert.Equal([workItemId], publisher.ExternalWorkIds);
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
        null,
        state);

    private sealed class FakeQueries : IImageIntakeStore, IIntakeReceiptQueries
    {
        public Task<IReadOnlyList<ImageIntakeSummary>> ListPendingPairingAsync(
            int maximumItems, Guid? caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>(Unassociated
                .Where(item => item.State == ImageInitiatedCaseState.AwaitingInstruction)
                .Take(maximumItems).ToArray());

        public IReadOnlyList<ImageIntakeSummary> Unassociated { get; init; } = [];

        public Dictionary<Guid, ImageIntakeDetail> ByOriginReceipt { get; init; } = [];

        public List<MergeImageInitiatedCaseRequest> Merges { get; } = [];

        public Guid? PendingExternalWorkId { get; set; }

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
            var source = ByOriginReceipt.Values.SingleOrDefault(item => item.Record.Id == request.ImageIntakeId);
            var originId = source?.Record.Origin.ReceiptId
                ?? Unassociated.Single(item => item.Id == request.ImageIntakeId).OriginReceiptId;
            var updated = new ImageIntakeRecord(request.ImageIntakeId,
                new(originId, new(IntakeSourceChannel.Mailbox, "token"), new string('a', 64), Guid.NewGuid()),
                "AB12CDE", "AB12CDE-01", ImageInitiatedCaseState.MergedIntoInstructionCase,
                MergedIntoCaseId: request.CaseId, PendingExternalWorkId: PendingExternalWorkId);
            ByOriginReceipt[originId] = new(updated, DateTimeOffset.UtcNow, request.CaseId, "QDS26001");
            return Task.FromResult(updated);
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
            CancellationToken cancellationToken)
        {
            if (ByOriginReceipt.TryGetValue(intakeReceiptId, out var detail))
            {
                return Task.FromResult<ImageIntakeDetail?>(detail);
            }
            var summary = Unassociated.SingleOrDefault(item => item.OriginReceiptId == intakeReceiptId);
            return Task.FromResult(summary is null ? null : new ImageIntakeDetail(
                new(summary.Id, new(intakeReceiptId,
                    new(IntakeSourceChannel.Mailbox, "token"), new string('a', 64), Guid.NewGuid()),
                    summary.NormalizedVehicleRegistration, summary.ImageIntakeReference,
                    summary.State, PrincipalId: summary.PrincipalId),
                summary.RegisteredAtUtc, summary.AssociatedCaseId, summary.AssociatedCaseReference));
        }

        Task<IntakeReceipt?> IIntakeReceiptQueries.GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var summary = Unassociated.SingleOrDefault(item => item.OriginReceiptId == id);
            var caseId = ByOriginReceipt.GetValueOrDefault(id)?.AssociatedCaseId ?? summary?.AssociatedCaseId;
            return Task.FromResult<IntakeReceipt?>(new(
                id, "retained.png", "image/png", 1, new string('a', 64),
                new(IntakeSourceChannel.Mailbox, "token"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                IntakeDecision.ImageIntakeRegistered, "Registered", [], [], null, [], null, null,
                false, "retained", "1", null, null,
                ManualLinkedCaseId: caseId, ManualAssociationVersion: caseId is null ? null : 0));
        }

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeListPage> ListAsync(IntakeDecision? decision, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeAssetRecord?> GetAssetAsync(Guid receiptId, Guid assetId, CancellationToken cancellationToken) => throw new NotSupportedException();

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
