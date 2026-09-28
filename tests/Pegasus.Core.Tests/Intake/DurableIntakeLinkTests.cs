using System.Security.Cryptography;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// A staff link (<see cref="LinkIntake"/>) is one owner: the association, then
/// what the link means — the linked material files on the Case in the same
/// request, and a filing this request cannot finish is handed to the Worker
/// (FRD-22). The link itself stands whatever the filing does.
/// </summary>
public sealed class DurableIntakeLinkTests
{
    private static readonly byte[] SourceBytes = [0x25, 0x50, 0x44, 0x46, 1, 2, 3, 4];
    private static readonly string SourceHash = Convert.ToHexString(SHA256.HashData(SourceBytes));
    private static readonly DateTimeOffset Now = new(2031, 9, 26, 13, 0, 0, TimeSpan.Zero);
    private const string StorageKey = "retained/upload.pdf";

    [Fact]
    public async Task StaffLinkFilesTheReceiptsEvidenceOnTheCaseInTheSameRequest()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var harness = new Harness(receipt, AutomaticCaseEvidencePromotionPreparationDisposition.Ready);

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        var link = Assert.Single(harness.MutationStore.Links);
        Assert.Equal(caseId, link.CaseId);
        var retained = Assert.Single(harness.Custody.Requests);
        Assert.Equal(caseId, retained.CaseId);
        Assert.Equal(receipt.Id, retained.IntakeReceiptId);
        Assert.True(retained.IsAutomaticIntakeEvidencePromotion);
        Assert.Equal(DocumentSource.Intake, retained.Source);
        Assert.Equal(DocumentSemanticRole.OriginalSource, retained.SemanticRole);
        Assert.Empty(harness.Enqueuer.StagedReceiptIds);
    }

    [Fact]
    public async Task StaffLinkHandsADeferredFilingToTheWorker()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var harness = new Harness(receipt, AutomaticCaseEvidencePromotionPreparationDisposition.Deferred);

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        Assert.Single(harness.MutationStore.Links);
        Assert.Empty(harness.Custody.Requests);
        Assert.Equal([harness.StagedReceiptId], harness.Enqueuer.StagedReceiptIds);
    }

    [Fact]
    public async Task StaffLinkStandsWhenTheFilingFailsAndTheWorkerRetriesIt()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var harness = new Harness(
            receipt,
            AutomaticCaseEvidencePromotionPreparationDisposition.Ready,
            custodyFailure: new InvalidOperationException("Box is unavailable."));

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        Assert.Single(harness.MutationStore.Links);
        Assert.Single(harness.Custody.Requests);
        Assert.Equal([harness.StagedReceiptId], harness.Enqueuer.StagedReceiptIds);
    }

    private static LinkIntakeRequest Request(IntakeReceipt receipt, Guid caseId) =>
        new(
            receipt.Id,
            caseId,
            receipt.Version,
            7,
            new string('l', 32),
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            $"upload-attach:{receipt.Id:N}",
            Reason: null);

    /// <summary>The receipt as the link's own read returns it: already associated to the Case by the staff decision just committed.</summary>
    private static IntakeReceipt LinkedReceipt(Guid id, Guid caseId) =>
        new(
            id,
            "upload.pdf",
            "application/pdf",
            SourceBytes.Length,
            SourceHash,
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, id.ToString("N")),
            Now,
            Now,
            IntakeDecision.NeedsSorting,
            "No accepted intake route established the principal for automatic case creation.",
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
            [
                new IntakeAssetRecord(
                    Guid.NewGuid(),
                    "uploaded source",
                    "upload.pdf",
                    "application/pdf",
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source,
                    SourceBytes.Length,
                    SourceHash,
                    StorageKey,
                    null,
                    null,
                    null,
                    null)
            ],
            Version: 2,
            ManualLinkedCaseId: caseId,
            ManualAssociationVersion: 0,
            ManualLinkedCaseReference: "QDOS26016",
            ManualAssociationActorKind: ActorKind.Staff,
            ManualAssociationOperationKey: $"upload-attach:{id:N}");

    private sealed class Harness
    {
        public Harness(
            IntakeReceipt receipt,
            AutomaticCaseEvidencePromotionPreparationDisposition preparation,
            Exception? custodyFailure = null)
        {
            StagedReceiptId = Guid.NewGuid();
            MutationStore = new RecordingMutationStore();
            Custody = new RecordingCustody(custodyFailure);
            Enqueuer = new RecordingEnqueuer();
            var promote = new PromoteAssociatedIntakeCaseEvidence(
                new ArtifactStore(StorageKey, SourceBytes),
                Custody,
                new PromotionStore(preparation));
            Link = new LinkIntake(
                MutationStore,
                new NoPairing(),
                TimeProvider.System,
                receiptQueries: new ReceiptQueries(receipt),
                promoteCaseEvidence: promote,
                workStore: new WorkStore(receipt.Id, StagedReceiptId),
                workEnqueuer: Enqueuer);
        }

        public Guid StagedReceiptId { get; }

        public RecordingMutationStore MutationStore { get; }

        public RecordingCustody Custody { get; }

        public RecordingEnqueuer Enqueuer { get; }

        public LinkIntake Link { get; }
    }

    private sealed class RecordingMutationStore : IIntakeMutationStore
    {
        public List<LinkIntakeRequest> Links { get; } = [];

        public Task<IntakeReceipt> ResolveAsync(
            ResolveIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleReevaluationAsync(
            ReevaluateIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt> ScheduleOcrRetryAsync(
            RetryIntakeOcrRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LinkAsync(LinkIntakeRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken)
        {
            Links.Add(request);
            return Task.CompletedTask;
        }

        public Task ReverseLinkAsync(
            ReverseIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AutoLinkAsync(
            AutomaticIntakeLinkRequest request, DateTimeOffset occurredAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoPairing : IImageIntakeCasePairing
    {
        public Task<ImageIntakePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakePairingResult> PairRegisteredReceiptAsync(Guid receiptId, CancellationToken cancellationToken) =>
            Task.FromResult(new ImageIntakePairingResult(0, 0, 0));

        public Task<ImageIntakePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ReceiptQueries(IntakeReceipt receipt) : IIntakeReceiptQueries
    {
        public Task<IntakeQueueCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(id == receipt.Id ? receipt : null);

        public Task<IntakeAssetRecord?> GetAssetAsync(Guid receiptId, Guid assetId, CancellationToken cancellationToken) =>
            Task.FromResult(receiptId == receipt.Id ? receipt.AssetRecords.FirstOrDefault(asset => asset.Id == assetId) : null);
    }

    private sealed class ArtifactStore(string storageKey, byte[] content) : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream value, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(key == storageKey ? content : null);
    }

    private sealed class RecordingCustody(Exception? failure) : ICaseArtifactCustody
    {
        public List<CaseArtifactCustodyRequest> Requests { get; } = [];

        public Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (failure is not null)
            {
                throw failure;
            }

            return Task.FromResult(new CaseArtifactCustodyResult(
                CaseArtifactCustodyDisposition.Confirmed,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "box-file",
                "box-version",
                request.Sha256,
                request.ContentLength,
                request.MediaType,
                null,
                null));
        }
    }

    private sealed class PromotionStore(AutomaticCaseEvidencePromotionPreparationDisposition disposition)
        : IAutomaticCaseEvidencePromotionStore
    {
        public Task<AutomaticCaseEvidencePromotionPreparation> PrepareAsync(
            AutomaticCaseEvidencePromotionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(disposition == AutomaticCaseEvidencePromotionPreparationDisposition.Ready
                ? new AutomaticCaseEvidencePromotionPreparation(disposition, request.CaseId, 7)
                : new AutomaticCaseEvidencePromotionPreparation(disposition));
    }

    private sealed class RecordingEnqueuer : IIntakeWorkEnqueuer
    {
        public List<Guid> StagedReceiptIds { get; } = [];

        public Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
        {
            StagedReceiptIds.Add(stagedReceiptId);
            return Task.CompletedTask;
        }
    }

    /// <summary>Only the staged-id lookup the link's Worker hand-off needs; nothing else is this test's concern.</summary>
    private sealed class WorkStore(Guid receiptId, Guid stagedReceiptId) : IIntakeWorkStore
    {
        public Task<Guid?> FindStagedReceiptIdForReceiptAsync(Guid intakeReceiptId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(intakeReceiptId == receiptId ? stagedReceiptId : null);

        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(
            IntakeSourceIdentity sourceIdentity, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReceivedIntake> ReceiveAsync(
            IntakeStagedReceipt receipt, string operationKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            Guid stagedReceipt, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkDispatchedAsync(
            Guid workItemId, string leaseToken, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseDispatchAsync(
            Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(
            Guid stagedReceipt, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeEvaluationRevision> RecordEvaluationAsync(
            Guid workItemId, string leaseToken, Guid processedReceiptId, DateTimeOffset completedAtUtc,
            bool isReevaluation, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task CompleteProcessingAsync(
            Guid workItemId, string leaseToken, DateTimeOffset completedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(
            Guid stagedReceipt, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RetryProcessingAsync(
            Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, string failureCode, bool terminal,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkPoisonedAsync(Guid stagedReceipt, DateTimeOffset failedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<int> RecoverInterruptedWorkAsync(
            DateTimeOffset nowUtc, DateTimeOffset staleDispatchedBeforeUtc, int maximumItems,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ScheduleReevaluationAsync(Guid stagedReceipt, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
