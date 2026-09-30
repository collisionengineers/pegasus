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
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch)]
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
                new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch),
                new(Guid.NewGuid(), "QDS26002", 0, "AB12CDE", DateTimeOffset.UnixEpoch)
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
            Result = [new(CaseId, "QDS26001", 0, "AB12CDEF", DateTimeOffset.UnixEpoch)]
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
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch)]
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
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void RegisteredSelectionPreservesExactIdentityAndPersistedGroupRule(
        int expectedMembers, bool selected)
    {
        ImageIntakeCaseCandidate[] candidates =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch),
            new(Guid.NewGuid(), "QDS26002", 0, "AB12CDEF", DateTimeOffset.UnixEpoch)
        ];
        var result = ImageIntakeCasePairing.SelectRegisteredTarget(candidates,
            "AB12CDE", null, expectedMembers, IntakeSourceChannel.Mailbox, DateTimeOffset.UtcNow);
        Assert.Equal(selected, result is not null);
    }

    [Fact]
    public void AKnownPrincipalScopesTheCandidatesBeforeTheUniquenessCount()
    {
        var principalId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        ImageIntakeCaseCandidate[] candidates =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch, principalId),
            new(otherCaseId, "QDS26002", 0, "AB12CDE", DateTimeOffset.UnixEpoch, Guid.NewGuid())
        ];

        // The same vehicle instructed by two Principals is a tie only while
        // the Principal is unknown.
        Assert.Null(ImageIntakeCasePairing.SelectRegisteredTarget(
            candidates, "AB12CDE", null, 1, IntakeSourceChannel.Mailbox, Registered));
        var selected = ImageIntakeCasePairing.SelectRegisteredTarget(
            candidates, "AB12CDE", principalId, 1, IntakeSourceChannel.Mailbox, Registered);
        Assert.Equal(CaseId, selected?.CaseId);
    }

    [Fact]
    public void AKnownPrincipalWithNoCaseOfItsOwnSelectsNothing()
    {
        ImageIntakeCaseCandidate[] candidates =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch, Guid.NewGuid())
        ];

        Assert.Null(ImageIntakeCasePairing.SelectRegisteredTarget(
            candidates, "AB12CDE", Guid.NewGuid(), 1, IntakeSourceChannel.Mailbox, Registered));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void AGroupCountsOnlyTheKnownPrincipalsCandidates(bool principalKnown, bool selected)
    {
        var principalId = Guid.NewGuid();
        ImageIntakeCaseCandidate[] candidates =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch, principalId),
            new(Guid.NewGuid(), "QDS26002", 0, "AB12CDEF", DateTimeOffset.UnixEpoch, Guid.NewGuid())
        ];

        var result = ImageIntakeCasePairing.SelectRegisteredTarget(
            candidates, "AB12CDE", principalKnown ? principalId : null, 2,
            IntakeSourceChannel.Mailbox, Registered);

        Assert.Equal(selected, result is not null);
    }

    [Fact]
    public void ExplainingWithheldAutomationUsesTheSameScopeAsSelection()
    {
        var principalId = Guid.NewGuid();
        ImageIntakeCaseCandidate[] tie =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch, principalId),
            new(Guid.NewGuid(), "QDS26002", 0, "AB12CDE", DateTimeOffset.UnixEpoch, Guid.NewGuid())
        ];
        ImageIntakeCaseCandidate[] sameTwice =
        [
            tie[0], new(Guid.NewGuid(), "QDS26003", 0, "AB12CDE", DateTimeOffset.UnixEpoch, principalId)
        ];
        ImageIntakeCaseCandidate[] group =
        [
            tie[0], new(Guid.NewGuid(), "QDS26004", 0, "AB12CDEF", DateTimeOffset.UnixEpoch, principalId)
        ];

        Assert.Equal(ImageIntakeAutomationWithheld.RegistrationAmbiguous,
            ImageIntakeCasePairing.ExplainWithheld(tie, "AB12CDE", null, 1));
        Assert.Null(ImageIntakeCasePairing.ExplainWithheld(tie, "AB12CDE", principalId, 1));
        Assert.Equal(ImageIntakeAutomationWithheld.RegistrationAmbiguous,
            ImageIntakeCasePairing.ExplainWithheld(sameTwice, "AB12CDE", principalId, 1));
        Assert.Equal(ImageIntakeAutomationWithheld.PrincipalDisagrees,
            ImageIntakeCasePairing.ExplainWithheld(tie, "AB12CDE", Guid.NewGuid(), 1));
        Assert.Equal(ImageIntakeAutomationWithheld.RegistrationAmbiguous,
            ImageIntakeCasePairing.ExplainWithheld(group, "AB12CDE", principalId, 2));
        Assert.Null(ImageIntakeCasePairing.ExplainWithheld([], "AB12CDE", Guid.NewGuid(), 1));
        Assert.Null(ImageIntakeCasePairing.ExplainWithheld(
            [new(CaseId, "QDS26001", 0, "ZZ99ZZZ", DateTimeOffset.UnixEpoch, principalId)],
            "AB12CDE", Guid.NewGuid(), 1));
    }

    [Theory]
    [InlineData(IntakeSourceChannel.ManualUpload, -1, false)]
    [InlineData(IntakeSourceChannel.ManualUpload, 0, false)]
    [InlineData(IntakeSourceChannel.ManualUpload, 1, true)]
    [InlineData(IntakeSourceChannel.Mailbox, -1, true)]
    [InlineData(IntakeSourceChannel.PrincipalApi, -1, true)]
    public void AManualUploadSelectsOnlyACaseCreatedAfterItRegistered(
        IntakeSourceChannel channel, int caseCreatedMinutesAfterRegistration, bool selected)
    {
        ImageIntakeCaseCandidate[] candidates =
        [
            new(CaseId, "QDS26001", 0, "AB12CDE", Registered.AddMinutes(caseCreatedMinutesAfterRegistration))
        ];

        var result = ImageIntakeCasePairing.SelectRegisteredTarget(
            candidates, "AB12CDE", null, 1, channel, Registered);

        Assert.Equal(selected, result is not null);
    }

    [Fact]
    public void AManualUploadWithAnOlderAndANewerExactMatchSelectsNeither()
    {
        ImageIntakeCaseCandidate[] candidates =
        [
            new(Guid.NewGuid(), "QDS26001", 0, "AB12CDE", Registered.AddMinutes(-5)),
            new(CaseId, "QDS26002", 0, "AB12CDE", Registered.AddMinutes(5))
        ];

        Assert.Null(ImageIntakeCasePairing.SelectRegisteredTarget(
            candidates, "AB12CDE", null, 1, IntakeSourceChannel.ManualUpload, Registered));
    }

    [Fact]
    public async Task ManualUploadPairsWithACaseAcceptedAfterItRegistered()
    {
        var receipt = Summary("AB12CDE-01", "AB12CDE", registeredAtUtc: Registered);
        var queries = new FakeQueries
        {
            Unassociated = [receipt],
            OriginChannel = IntakeSourceChannel.ManualUpload
        };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", Registered.AddMinutes(3))]
        };
        var mutationStore = new FakeMutationStore();

        var result = await new ImageIntakeCasePairing(
                queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 1, 0), result);
        var link = Assert.Single(mutationStore.AutoLinks);
        Assert.Equal(receipt.OriginReceiptId, link.ReceiptId);
        Assert.Equal(CaseId, link.CaseId);
        Assert.Equal(ActorKind.SystemWorker, link.Actor.Kind);
        Assert.Equal(CaseId, Assert.Single(queries.Merges).CaseId);
    }

    [Fact]
    public async Task ManualUploadPairsWithALaterCaseThroughTheRecoverySweep()
    {
        var receipt = Summary("AB12CDE-01", "AB12CDE", registeredAtUtc: Registered);
        var queries = new FakeQueries
        {
            Unassociated = [receipt],
            OriginChannel = IntakeSourceChannel.ManualUpload
        };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", Registered.AddMinutes(3))]
        };
        var mutationStore = new FakeMutationStore();

        var result = await new ImageIntakeCasePairing(
                queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .ReconcileAsync(50, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 1, 0), result);
        Assert.Equal(CaseId, Assert.Single(mutationStore.AutoLinks).CaseId);
    }

    [Fact]
    public async Task ManualUploadWithAnOlderAndANewerMatchWaitsForStaff()
    {
        var queries = new FakeQueries
        {
            Unassociated = [Summary("AB12CDE-01", "AB12CDE", registeredAtUtc: Registered)],
            OriginChannel = IntakeSourceChannel.ManualUpload
        };
        var candidates = new FakeCandidates
        {
            Result =
            [
                new(Guid.NewGuid(), "QDS26001", 0, "AB12CDE", Registered.AddMinutes(-3)),
                new(CaseId, "QDS26002", 0, "AB12CDE", Registered.AddMinutes(3))
            ]
        };
        var mutationStore = new FakeMutationStore();

        var result = await new ImageIntakeCasePairing(
                queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 0, 0), result);
        Assert.Empty(mutationStore.AutoLinks);
    }

    [Fact]
    public async Task AStaffStartedManualGroupGainsNoAutomaticLinkFromALaterCase()
    {
        var siblingReceiptId = Guid.NewGuid();
        var receipt = Summary("AB12CDE-01", "AB12CDE", registeredAtUtc: Registered);
        var queries = new FakeQueries
        {
            Unassociated = [receipt],
            OriginChannel = IntakeSourceChannel.ManualUpload,
            Images = [new(siblingReceiptId, "sibling.png", "image/png")]
        };
        queries.ManualLinkedCaseIds[siblingReceiptId] = CaseId;
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", Registered.AddMinutes(3))]
        };
        var mutationStore = new FakeMutationStore();

        var result = await new ImageIntakeCasePairing(
                queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .PairRegisteredReceiptAsync(receipt.OriginReceiptId, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 0, 0), result);
        Assert.Empty(mutationStore.AutoLinks);
        Assert.Empty(queries.Merges);
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
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch)]
        };
        var mutationStore = new FakeMutationStore();

        await new ImageIntakeCasePairing(queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .PairAcceptedCaseAsync(CaseId, CancellationToken.None);

        Assert.Empty(mutationStore.AutoLinks);
    }

    [Fact]
    public async Task ManualUploadWithACaseThatAlreadyExistedRemainsPendingThroughTheRecoverySweep()
    {
        // The Case (created at the Unix epoch) existed when the images
        // registered, so the upload offered it and it stays a staff decision.
        var receipt = Summary("AB12CDE-01", "AB12CDE");
        var queries = new FakeQueries
        {
            Unassociated = [receipt],
            OriginChannel = IntakeSourceChannel.ManualUpload
        };
        var candidates = new FakeCandidates
        {
            Result = [new(CaseId, "QDS26001", 0, "AB12CDE", DateTimeOffset.UnixEpoch)]
        };
        var mutationStore = new FakeMutationStore();

        var result = await new ImageIntakeCasePairing(
                queries, candidates, mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries)
            .ReconcileAsync(50, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 0, 0), result);
        Assert.Empty(mutationStore.AutoLinks);
    }

    [Fact]
    public async Task ManualGroupMergesOnlyAfterStaffHasLinkedEveryRegisteredImage()
    {
        var receiptId = Guid.NewGuid();
        var siblingReceiptId = Guid.NewGuid();
        var origin = new ImageIntakeOrigin(
            receiptId,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "manual-image"),
            new string('a', 64),
            Guid.NewGuid());
        var detail = new ImageIntakeDetail(
            new ImageIntakeRecord(Guid.NewGuid(), origin, "AB12CDE", "AB12CDE-01"),
            DateTimeOffset.UtcNow,
            CaseId,
            "QDS26001",
            AssociatedCaseVersion: 7);
        var queries = new FakeQueries
        {
            OriginChannel = IntakeSourceChannel.ManualUpload,
            ByOriginReceipt = { [receiptId] = detail },
            Images = [new(siblingReceiptId, "sibling.png", "image/png")]
        };
        var mutationStore = new FakeMutationStore();
        var pairing = new ImageIntakeCasePairing(
            queries, new FakeCandidates(), mutationStore, TimeProvider.System, new CommittedWorkPublisherDouble(), queries);

        var deferred = await pairing.PairRegisteredReceiptAsync(receiptId, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 0, 0), deferred);
        Assert.Empty(queries.Merges);
        Assert.Empty(mutationStore.AutoLinks);

        queries.ManualLinkedCaseIds[siblingReceiptId] = CaseId;

        var result = await pairing.PairRegisteredReceiptAsync(receiptId, CancellationToken.None);

        Assert.Equal(new ImageIntakePairingResult(1, 1, 0), result);
        Assert.Empty(mutationStore.AutoLinks);
        var merged = Assert.Single(queries.Merges);
        Assert.Equal(detail.Record.Id, merged.ImageIntakeId);
        Assert.Equal(CaseId, merged.CaseId);
        Assert.Equal(0, merged.ExpectedVersion);
        Assert.Equal(0, merged.ExpectedStaffOriginAssociationVersion);
        var completed = Assert.IsType<ImageIntakeDetail>(
            await queries.GetByOriginReceiptAsync(receiptId, CancellationToken.None));
        Assert.Equal(ImageInitiatedCaseState.MergedIntoInstructionCase, completed.State);
        Assert.Equal(CaseId, completed.MergedIntoCaseId);
        Assert.Equal(origin, completed.Record.Origin);
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

    private static readonly DateTimeOffset Registered = new(2026, 9, 28, 12, 25, 53, TimeSpan.Zero);

    private static ImageIntakeSummary Summary(
        string reference,
        string registration,
        ImageInitiatedCaseState state = ImageInitiatedCaseState.AwaitingInstruction,
        Guid? associatedCaseId = null,
        DateTimeOffset? registeredAtUtc = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        reference,
        registration,
        associatedCaseId,
        null,
        registeredAtUtc ?? DateTimeOffset.UtcNow,
        null,
        state);

    private sealed class FakeQueries : IImageIntakeStore, IIntakeReceiptQueries
    {
        public IntakeSourceChannel OriginChannel { get; init; } = IntakeSourceChannel.Mailbox;

        public Task<IReadOnlyList<ImageIntakeSummary>> ListPendingPairingAsync(
            int maximumItems, Guid? caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>(Unassociated
                .Where(item => item.State == ImageInitiatedCaseState.AwaitingInstruction)
                .Take(maximumItems).ToArray());

        public IReadOnlyList<ImageIntakeSummary> Unassociated { get; init; } = [];

        public IReadOnlyList<ImageIntakeImage> Images { get; init; } = [];

        public Dictionary<Guid, ImageIntakeDetail> ByOriginReceipt { get; init; } = [];

        public Dictionary<Guid, Guid?> ManualLinkedCaseIds { get; } = [];

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
            var origin = source?.Record.Origin
                ?? new(originId, new(OriginChannel, "token"), new string('a', 64), Guid.NewGuid());
            var updated = new ImageIntakeRecord(request.ImageIntakeId,
                origin,
                "AB12CDE", "AB12CDE-01", ImageInitiatedCaseState.MergedIntoInstructionCase,
                MergedIntoCaseId: request.CaseId, PendingExternalWorkId: PendingExternalWorkId);
            ByOriginReceipt[originId] = new(updated, DateTimeOffset.UtcNow, request.CaseId, "QDS26001");
            return Task.FromResult(updated);
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            ImageInitiatedCaseState? state,
            CancellationToken cancellationToken)
        {
            Assert.Null(associated);
            return Task.FromResult(Unassociated);
        }

        public Task<IReadOnlyList<ImageIntakeImage>> ListImagesAsync(
            Guid imageIntakeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Images);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>> ListImagesAsync(
            IReadOnlyCollection<Guid> imageIntakeIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>>>(
                imageIntakeIds.Distinct().ToDictionary(
                    imageIntakeId => imageIntakeId,
                    _ => Images));

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
                    new(OriginChannel, "token"), new string('a', 64), Guid.NewGuid()),
                    summary.NormalizedVehicleRegistration, summary.ImageIntakeReference,
                    summary.State, PrincipalId: summary.PrincipalId),
                summary.RegisteredAtUtc, summary.AssociatedCaseId, summary.AssociatedCaseReference));
        }

        Task<IntakeReceipt?> IIntakeReceiptQueries.GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var summary = Unassociated.SingleOrDefault(item => item.OriginReceiptId == id);
            var caseId = ManualLinkedCaseIds.GetValueOrDefault(id)
                ?? ByOriginReceipt.GetValueOrDefault(id)?.AssociatedCaseId
                ?? summary?.AssociatedCaseId;
            return Task.FromResult<IntakeReceipt?>(new(
                id, "retained.png", "image/png", 1, new string('a', 64),
                new(OriginChannel, "token"), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                IntakeDecision.ImageIntakeRegistered, "Registered", [], [], null, [], null, null,
                false, "retained", "1", null, null,
                ManualLinkedCaseId: caseId,
                ManualAssociationVersion: caseId is null ? null : 0,
                ManualAssociationActorKind: caseId is not null && OriginChannel == IntakeSourceChannel.ManualUpload
                    ? ActorKind.Staff
                    : null));
        }

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

        public Task<IntakeReceipt> ScheduleOcrRetryAsync(
            RetryIntakeOcrRequest request,
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
