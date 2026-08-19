using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class AutomaticImageIntakeTests
{
    private static readonly byte[] ImageBytes = [1, 2, 3, 4, 5];
    private static readonly string ImageHash = Convert.ToHexString(SHA256.HashData(ImageBytes));

    [Fact]
    public async Task OneConfidentReadRegistersAndAssociatesTheSingleEligibleCase()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));
        harness.CaseCandidates.Candidates =
        [
            new(Guid.NewGuid(), "QDS26001", 3, "AB12CDE")
        ];

        await harness.ApplyAsync();

        var registration = Assert.Single(harness.Register.Requests);
        Assert.Equal("AB12CDE", registration.NormalizedVehicleRegistration);
        Assert.Equal(ActorKind.SystemWorker, registration.Actor.Kind);
        var link = Assert.Single(harness.MutationStore.AutoLinks);
        Assert.Equal(harness.Receipt.Id, link.ReceiptId);
        Assert.Equal(harness.CaseCandidates.Candidates[0].CaseId, link.CaseId);
        Assert.Equal(3, link.ExpectedCaseVersion);
        var suggestion = Assert.Single(harness.SuggestionStore.Records);
        Assert.Equal(VrmRecognitionOutcomeKind.Suggested, suggestion.Outcome);
        Assert.Contains(
            harness.SuggestionStore.Dispositions,
            disposition => disposition.Disposition == ImageVrmSuggestionDisposition.Confirmed);
    }

    [Fact]
    public async Task NoCandidateCaseRegistersWithoutAssociating()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));

        await harness.ApplyAsync();

        Assert.Single(harness.Register.Requests);
        Assert.Empty(harness.MutationStore.AutoLinks);
    }

    [Fact]
    public async Task AmbiguousCandidatesRegisterWithoutAssociating()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));
        harness.CaseCandidates.Candidates =
        [
            new(Guid.NewGuid(), "QDS26001", 1, "AB12CDE"),
            new(Guid.NewGuid(), "QDS26002", 1, "AB12CDE")
        ];

        await harness.ApplyAsync();

        Assert.Single(harness.Register.Requests);
        Assert.Empty(harness.MutationStore.AutoLinks);
    }

    [Fact]
    public async Task BelowBarReadRecordsTheSuggestionOnly()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.50));

        await harness.ApplyAsync();

        Assert.Empty(harness.Register.Requests);
        Assert.Empty(harness.MutationStore.AutoLinks);
        var suggestion = Assert.Single(harness.SuggestionStore.Records);
        Assert.Equal(ImageVrmSuggestionDisposition.Pending, harness.SuggestionStore.Records[0].Disposition);
        Assert.Equal("AB12CDE", suggestion.SuggestedRegistration);
    }

    [Fact]
    public async Task TwoDistinctConfidentReadsAbstainFromRegistering()
    {
        var harness = new Harness(assetCount: 2);
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));
        harness.Engine.Enqueue(Suggested("XY34ZZZ", 0.95));

        await harness.ApplyAsync();

        Assert.Empty(harness.Register.Requests);
        Assert.Empty(harness.MutationStore.AutoLinks);
        Assert.Equal(2, harness.SuggestionStore.Records.Count);
    }

    [Fact]
    public async Task UnavailableEngineRecordsTheOutcomeAndNeverBlocks()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(new VrmRecognitionResult(
            VrmRecognitionOutcomeKind.Unavailable,
            [],
            "fast-alpr-onnx",
            "1",
            string.Empty,
            "engine_unavailable",
            "The engine dependency could not be initialised."));

        var receipt = await harness.ApplyAsync();

        Assert.Empty(harness.Register.Requests);
        var suggestion = Assert.Single(harness.SuggestionStore.Records);
        Assert.Equal(VrmRecognitionOutcomeKind.Unavailable, suggestion.Outcome);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
    }

    [Fact]
    public async Task AlreadyAssociatedReceiptRegistersWithoutRelinking()
    {
        var harness = new Harness(manualLinkedCaseId: Guid.NewGuid());
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));
        harness.CaseCandidates.Candidates = [new(Guid.NewGuid(), "QDS26001", 1, "AB12CDE")];

        await harness.ApplyAsync();

        Assert.Single(harness.Register.Requests);
        Assert.Empty(harness.MutationStore.AutoLinks);
    }

    [Fact]
    public async Task TruncatedReadCompletesFromTheSingleCandidatesConfirmedRegistration()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("BX69YL", 0.95));
        harness.CaseCandidates.Candidates =
        [
            new(Guid.NewGuid(), "QDS26003", 2, "BX69YLM")
        ];

        await harness.ApplyAsync();

        var registration = Assert.Single(harness.Register.Requests);
        Assert.Equal("BX69YLM", registration.NormalizedVehicleRegistration);
        var link = Assert.Single(harness.MutationStore.AutoLinks);
        Assert.Equal(harness.CaseCandidates.Candidates[0].CaseId, link.CaseId);
        Assert.Contains(
            harness.SuggestionStore.Dispositions,
            disposition => disposition.Disposition == ImageVrmSuggestionDisposition.Confirmed);
    }

    [Fact]
    public async Task ExactCandidateBeatsAOneCharacterMissingCandidate()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("BX69YLM", 0.95));
        var exactCaseId = Guid.NewGuid();
        harness.CaseCandidates.Candidates =
        [
            new(exactCaseId, "QDS26004", 1, "BX69YLM"),
            new(Guid.NewGuid(), "QDS26005", 1, "BX69YLMA")
        ];

        await harness.ApplyAsync();

        var registration = Assert.Single(harness.Register.Requests);
        Assert.Equal("BX69YLM", registration.NormalizedVehicleRegistration);
        var link = Assert.Single(harness.MutationStore.AutoLinks);
        Assert.Equal(exactCaseId, link.CaseId);
    }

    [Fact]
    public async Task AnInsertedFifthPositionOneCompletesFromTheConfirmedRegistration()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("PK201YHR", 0.95));
        harness.CaseCandidates.Candidates =
        [
            new(Guid.NewGuid(), "QDS26008", 2, "PK20YHR")
        ];

        await harness.ApplyAsync();

        var registration = Assert.Single(harness.Register.Requests);
        Assert.Equal("PK20YHR", registration.NormalizedVehicleRegistration);
        var link = Assert.Single(harness.MutationStore.AutoLinks);
        Assert.Equal(harness.CaseCandidates.Candidates[0].CaseId, link.CaseId);
    }

    [Fact]
    public async Task TwoOneCharacterMissingCandidatesAreAmbiguous()
    {
        var harness = new Harness();
        harness.Engine.Enqueue(Suggested("BX69YL", 0.95));
        harness.CaseCandidates.Candidates =
        [
            new(Guid.NewGuid(), "QDS26006", 1, "BX69YLM"),
            new(Guid.NewGuid(), "QDS26007", 1, "BX69YLP")
        ];

        await harness.ApplyAsync();

        // Registration proceeds with the read itself; no association and no
        // guessed completion between the two candidates.
        var registration = Assert.Single(harness.Register.Requests);
        Assert.Equal("BX69YL", registration.NormalizedVehicleRegistration);
        Assert.Empty(harness.MutationStore.AutoLinks);
    }

    [Fact]
    public async Task InstructionBearingReceiptIsNeverScanned()
    {
        var harness = new Harness(mediaType: "application/pdf");

        await harness.ApplyAsync();

        Assert.Equal(0, harness.Engine.Calls);
        Assert.Empty(harness.SuggestionStore.Records);
        Assert.Empty(harness.Register.Requests);
    }

    [Fact]
    public async Task ExistingRegistrationShortCircuitsTheScan()
    {
        var harness = new Harness();
        harness.ImageIntakeQueries.Existing = new ImageIntakeDetail(
            new(
                Guid.NewGuid(),
                new(
                    harness.Receipt.Id,
                    harness.Receipt.SourceIdentity,
                    harness.Receipt.SourceHash.ToLowerInvariant(),
                    Guid.NewGuid()),
                "AB12CDE",
                "AB12CDE-01"),
            DateTimeOffset.UtcNow,
            null,
            null);

        await harness.ApplyAsync();

        Assert.Equal(0, harness.Engine.Calls);
        Assert.Empty(harness.Register.Requests);
    }

    [Fact]
    public async Task AmbiguousGroupEligibilityHandsOffDespiteAPerMemberExactMatch()
    {
        // Both members read the same registration, but the group holds two
        // raw candidates for it — one an exact match, one a one-character-
        // longer fuzzy match. The group-level count of two is ambiguous, so
        // the group hands off to ImageIntake. The per-member candidate
        // search below would, on its own, resolve the ambiguity by exact
        // match and associate — that must never overrule the group decision.
        var harness = new GroupHarness(memberCount: 2);
        harness.Engine.Enqueue(Suggested("BX69YLM", 0.95));
        harness.Engine.Enqueue(Suggested("BX69YLM", 0.95));
        var exactCaseId = Guid.NewGuid();
        harness.CaseCandidates.Candidates =
        [
            new(exactCaseId, "QDS26010", 1, "BX69YLM"),
            new(Guid.NewGuid(), "QDS26011", 1, "BX69YLMA")
        ];

        await harness.ApplyAsync(triggerOrdinal: 1);

        Assert.Empty(harness.MutationStore.AutoLinks);
        Assert.Equal(2, harness.Register.Requests.Count);
    }

    [Fact]
    public async Task RecognitionRunsOnceEvenWhenTheGroupIsTriggeredTwice()
    {
        var harness = new GroupHarness(memberCount: 2);
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));

        await harness.ApplyAsync(triggerOrdinal: 1);
        await harness.ApplyAsync(triggerOrdinal: 1);

        Assert.Equal(2, harness.Engine.Calls);
    }

    [Fact]
    public async Task AGroupWithFewerStagedMembersThanDeclaredNeverFinalizes()
    {
        // The originating submission declared three files; only the one
        // that triggered automation has landed as a member row so far. The
        // group must not be evaluated as complete just because every row
        // that happens to exist also has a receipt.
        var harness = new GroupHarness(memberCount: 1, expectedMemberCount: 3);
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));

        await harness.ApplyAsync(triggerOrdinal: 0);

        Assert.Empty(harness.Register.Requests);
        Assert.Empty(harness.MutationStore.AutoLinks);
    }

    [Fact]
    public async Task ANonImageGroupMemberIsExcludedFromRecognitionAndDoesNotBlockTheDecision()
    {
        var harness = new GroupHarness(
            memberCount: 2,
            mediaTypeByOrdinal: new Dictionary<int, string> { [1] = "application/pdf" });
        harness.Engine.Enqueue(Suggested("AB12CDE", 0.95));
        harness.CaseCandidates.Candidates = [new(Guid.NewGuid(), "QDS26012", 1, "AB12CDE")];

        await harness.ApplyAsync(triggerOrdinal: 0);

        Assert.Equal(1, harness.Engine.Calls);
        Assert.Single(harness.Register.Requests);
        Assert.Single(harness.MutationStore.AutoLinks);
    }

    private static VrmRecognitionResult Suggested(string registration, double confidence) => new(
        VrmRecognitionOutcomeKind.Suggested,
        [new(registration, registration, confidence, null)],
        "fast-alpr-onnx",
        "1",
        "plate-detection=abc;plate-recognition=def");

    private sealed class Harness
    {
        public Harness(
            int assetCount = 1,
            string mediaType = "image/jpeg",
            Guid? manualLinkedCaseId = null)
        {
            var assets = Enumerable.Range(0, assetCount)
                .Select(index => new IntakeAssetRecord(
                    Guid.NewGuid(),
                    "uploaded source",
                    $"vehicle-{index}.jpg",
                    mediaType,
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    ImageBytes.Length,
                    ImageHash,
                    $"storage/{index}",
                    null,
                    null,
                    null,
                    null))
                .ToArray();
            Receipt = new IntakeReceipt(
                Guid.NewGuid(),
                "vehicle-0.jpg",
                mediaType,
                ImageBytes.Length,
                ImageHash,
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "receipt-token"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                IntakeDecision.NeedsSorting,
                "The readable content does not provide enough evidence to suggest a principal.",
                [],
                [],
                null,
                [],
                null,
                null,
                false,
                "intake_source_reader",
                "1",
                null,
                null,
                assets,
                ManualLinkedCaseId: manualLinkedCaseId,
                ManualAssociationVersion: manualLinkedCaseId is null ? null : 0);
            foreach (var asset in assets)
            {
                ArtifactStore.Content[asset.StorageKey] = ImageBytes;
            }

            Automation = new ImageIntakeAutomation(
                Engine,
                SuggestionStore,
                ArtifactStore,
                OriginResolver,
                ImageIntakeQueries,
                Register,
                CaseCandidates,
                MutationStore,
                ReceiptQueries,
                TimeProvider.System);
        }

        public IntakeReceipt Receipt { get; }

        public FakeEngine Engine { get; } = new();

        public FakeSuggestionStore SuggestionStore { get; } = new();

        public FakeArtifactStore ArtifactStore { get; } = new();

        public FakeOriginResolver OriginResolver { get; } = new();

        public FakeImageIntakeQueries ImageIntakeQueries { get; } = new();

        public FakeRegister Register { get; } = new();

        public FakeCaseCandidates CaseCandidates { get; } = new();

        public FakeMutationStore MutationStore { get; } = new();

        public FakeReceiptQueries ReceiptQueries { get; } = new();

        private ImageIntakeAutomation Automation { get; }

        public Task<IntakeReceipt> ApplyAsync()
        {
            OriginResolver.Origin = new ImageIntakeOrigin(
                Receipt.Id,
                Receipt.SourceIdentity,
                Receipt.SourceHash.ToLowerInvariant(),
                Guid.NewGuid());
            ReceiptQueries.Receipt = Receipt;
            return Automation.ApplyAsync(Receipt, CancellationToken.None);
        }
    }

    private sealed class FakeEngine : IVrmRecognitionEngine
    {
        private readonly Queue<VrmRecognitionResult> results = new();

        public int Calls { get; private set; }

        public void Enqueue(VrmRecognitionResult result) => results.Enqueue(result);

        public Task<VrmRecognitionResult> RecognizeAsync(
            ReadOnlyMemory<byte> imageBytes,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(results.Count > 0
                ? results.Dequeue()
                : new VrmRecognitionResult(
                    VrmRecognitionOutcomeKind.NoReadableResult,
                    [],
                    "fast-alpr-onnx",
                    "1",
                    string.Empty));
        }
    }

    private sealed class FakeSuggestionStore : IVrmSuggestionStore
    {
        public List<ImageVrmSuggestion> Records { get; } = [];

        public List<ImageVrmSuggestionDispositionRequest> Dispositions { get; } = [];

        public Task<ImageVrmSuggestion> RecordAsync(
            ImageVrmSuggestionDraft draft,
            CancellationToken cancellationToken)
        {
            var suggestion = new ImageVrmSuggestion(
                Guid.NewGuid(),
                draft.IntakeReceiptId,
                draft.IntakeAssetId,
                draft.StorageKey,
                draft.ContentHash,
                draft.EngineKey,
                draft.EngineVersion,
                draft.ModelHashes,
                draft.Outcome,
                draft.SuggestedRegistration,
                draft.Confidence,
                draft.FailureCode,
                draft.FailureReason,
                DateTimeOffset.UtcNow,
                ImageVrmSuggestionDisposition.Pending,
                null,
                null,
                null);
            Records.Add(suggestion);
            return Task.FromResult(suggestion);
        }

        public Task<IReadOnlyList<ImageVrmSuggestion>> ListForReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageVrmSuggestion>>(
                Records.Where(record => record.IntakeReceiptId == intakeReceiptId).ToArray());

        public Task<ImageVrmSuggestion?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Records.FirstOrDefault(record => record.Id == id));

        public Task<ImageVrmSuggestion> SetDispositionAsync(
            ImageVrmSuggestionDispositionRequest request,
            CancellationToken cancellationToken)
        {
            Dispositions.Add(request);
            var suggestion = Records.Single(record => record.Id == request.SuggestionId);
            return Task.FromResult(suggestion with { Disposition = request.Disposition });
        }
    }

    private sealed class FakeArtifactStore : IIntakeArtifactStore
    {
        public Dictionary<string, byte[]> Content { get; } = [];

        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(
                Content.TryGetValue(storageKey, out var bytes) ? bytes : null);
    }

    private sealed class FakeOriginResolver : IImageIntakeOriginResolver
    {
        public ImageIntakeOrigin? Origin { get; set; }

        public Dictionary<Guid, ImageIntakeOrigin> Origins { get; } = [];

        public Task<ImageIntakeOrigin?> ResolveOriginAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Origins.TryGetValue(intakeReceiptId, out var origin) ? origin : Origin);
    }

    private sealed class FakeImageIntakeQueries : IImageIntakeStore
    {
        public ImageIntakeDetail? Existing { get; set; }

        public int EnsureRegisteredCalls { get; private set; }

        public Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeOperationReplay?>(null);

        public Task<ImageIntakeRecord> RegisterAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task EnsureRegisteredReceiptDecisionAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken)
        {
            EnsureRegisteredCalls++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<ImageIntakeDetail?>(null);

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) => Task.FromResult(Existing);

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

    private sealed class FakeRegister : IRegisterImageIntake
    {
        public List<RegisterImageIntakeRequest> Requests { get; } = [];

        public Task<ImageIntakeRecord> ExecuteAsync(
            RegisterImageIntakeRequest request,
            CancellationToken cancellationToken)
        {
            ImageIntakeLifecycleRules.ValidateRegister(request);
            Requests.Add(request);
            return Task.FromResult(new ImageIntakeRecord(
                Guid.NewGuid(),
                request.Origin,
                request.NormalizedVehicleRegistration,
                ImageIntakeReferenceFormat.Create(request.NormalizedVehicleRegistration, 1)));
        }
    }

    private sealed class FakeCaseCandidates : IImageIntakeCaseCandidates
    {
        public IReadOnlyList<ImageIntakeCaseCandidate> Candidates { get; set; } = [];

        public Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken) => Task.FromResult(Candidates);
    }

    private sealed class FakeMutationStore : IIntakeMutationStore
    {
        public List<AutomaticIntakeLinkRequest> AutoLinks { get; } = [];

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
            AutoLinks.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReceiptQueries : IIntakeReceiptQueries
    {
        public IntakeReceipt? Receipt { get; set; }

        public List<IntakeReceipt> Receipts { get; } = [];

        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeQueueCounts(0, 0));

        public Task<IntakeListPage> ListAsync(
            IntakeDecision? decision,
            int page,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeListPage([], page, pageSize, 0));

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(
                Receipts.FirstOrDefault(receipt => receipt.Id == id)
                    ?? (Receipt?.Id == id ? Receipt : null));

        public Task<IntakeReceipt?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Receipts.FirstOrDefault(receipt => receipt.SourceIdentity == sourceIdentity)
                    ?? (Receipt is not null && Receipt.SourceIdentity == sourceIdentity ? Receipt : null));

        public Task<IntakeAssetRecord?> GetAssetAsync(
            Guid receiptId,
            Guid assetId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeAssetRecord?>(null);
    }

    private sealed class FakeGroupStore : IIntakeSubmissionGroupStore
    {
        public IntakeSubmissionGroup? Group { get; set; }

        public Task<IntakeSubmissionGroup?> GetAsync(
            Guid groupId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroup?> FindAsync(
            IntakeSourceChannel channel,
            string submissionToken,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroup?> FindForMemberSourceAsync(
            IntakeSourceIdentity sourceIdentity,
            CancellationToken cancellationToken = default) => Task.FromResult(Group);

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(
            Guid groupId,
            IntakeSourceChannel channel,
            string submissionToken,
            int expectedMemberCount,
            string actor,
            DateTimeOffset receivedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember?> FindMemberAsync(
            Guid groupId,
            int ordinal,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(
            Guid groupId,
            int ordinal,
            ReceivedIntake received,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
            Guid groupId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Group!.Members);
    }

    /// <summary>
    /// A completed multi-image group, wired the same way
    /// <see cref="ImageIntakeAutomation"/> discovers one in production: each
    /// member's receipt carries the <see cref="GroupedIntakeMemberToken"/>
    /// shape, and the group store resolves any member's source identity back
    /// to the shared group.
    /// </summary>
    private sealed class GroupHarness
    {
        private readonly ImageIntakeAutomation automation;

        public GroupHarness(
            int memberCount,
            int? expectedMemberCount = null,
            IReadOnlyDictionary<int, string>? mediaTypeByOrdinal = null)
        {
            const string submissionToken = "group-token";
            var groupId = Guid.NewGuid();
            var members = new List<IntakeSubmissionGroupMember>(memberCount);
            for (var ordinal = 0; ordinal < memberCount; ordinal++)
            {
                var mediaType = mediaTypeByOrdinal is not null
                    && mediaTypeByOrdinal.TryGetValue(ordinal, out var overriddenMediaType)
                        ? overriddenMediaType
                        : "image/jpeg";
                var asset = new IntakeAssetRecord(
                    Guid.NewGuid(),
                    "uploaded source",
                    $"vehicle-{ordinal}.jpg",
                    mediaType,
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    ImageBytes.Length,
                    ImageHash,
                    $"storage/group/{ordinal}",
                    null,
                    null,
                    null,
                    null);
                ArtifactStore.Content[asset.StorageKey] = ImageBytes;

                var token = GroupedIntakeMemberToken.Create(submissionToken, ordinal);
                var receipt = new IntakeReceipt(
                    Guid.NewGuid(),
                    $"vehicle-{ordinal}.jpg",
                    mediaType,
                    ImageBytes.Length,
                    ImageHash,
                    new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, token),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    IntakeDecision.NeedsSorting,
                    "The readable content does not provide enough evidence to suggest a principal.",
                    [],
                    [],
                    null,
                    [],
                    null,
                    null,
                    false,
                    "intake_source_reader",
                    "1",
                    null,
                    null,
                    [asset]);
                Receipts.Add(receipt);
                ReceiptQueries.Receipts.Add(receipt);
                OriginResolver.Origins[receipt.Id] = new ImageIntakeOrigin(
                    receipt.Id,
                    receipt.SourceIdentity,
                    receipt.SourceHash.ToLowerInvariant(),
                    Guid.NewGuid());
                members.Add(new IntakeSubmissionGroupMember(
                    groupId, ordinal, receipt.Id, receipt.SourceFileName, receipt.SourceHash, false));
            }

            GroupStore.Group = new IntakeSubmissionGroup(
                groupId,
                IntakeSourceChannel.ManualUpload,
                submissionToken,
                expectedMemberCount ?? memberCount,
                "staff:test",
                DateTimeOffset.UtcNow,
                members);

            automation = new ImageIntakeAutomation(
                Engine,
                SuggestionStore,
                ArtifactStore,
                OriginResolver,
                ImageIntakeQueries,
                Register,
                CaseCandidates,
                MutationStore,
                ReceiptQueries,
                TimeProvider.System,
                GroupStore);
        }

        public List<IntakeReceipt> Receipts { get; } = [];

        public FakeEngine Engine { get; } = new();

        public FakeSuggestionStore SuggestionStore { get; } = new();

        public FakeArtifactStore ArtifactStore { get; } = new();

        public FakeOriginResolver OriginResolver { get; } = new();

        public FakeImageIntakeQueries ImageIntakeQueries { get; } = new();

        public FakeRegister Register { get; } = new();

        public FakeCaseCandidates CaseCandidates { get; } = new();

        public FakeMutationStore MutationStore { get; } = new();

        public FakeReceiptQueries ReceiptQueries { get; } = new();

        public FakeGroupStore GroupStore { get; } = new();

        public Task<IntakeReceipt> ApplyAsync(int triggerOrdinal) =>
            automation.ApplyAsync(Receipts[triggerOrdinal], CancellationToken.None);
    }
}
