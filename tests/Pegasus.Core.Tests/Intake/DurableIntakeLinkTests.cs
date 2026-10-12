using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// A staff link (<see cref="LinkIntake"/>) is one owner: the association
/// commits and the receipt's Unidentified item is resolved, and filing the
/// linked material continues in the Worker's replay of the receipt's staged
/// work (FRD-22).
/// </summary>
public sealed class DurableIntakeLinkTests
{
    private static readonly byte[] SourceBytes = [0x25, 0x50, 0x44, 0x46, 1, 2, 3, 4];
    private static readonly string SourceHash = Convert.ToHexString(SHA256.HashData(SourceBytes));
    private static readonly DateTimeOffset Now = new(2031, 9, 26, 13, 0, 0, TimeSpan.Zero);
    private const string StorageKey = "retained/upload.pdf";

    [Fact]
    public async Task StaffLinkCommitsTheAssociationAndHandsFilingToTheWorker()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var harness = new Harness(receipt.Id);

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        var link = Assert.Single(harness.MutationStore.Links);
        Assert.Equal(caseId, link.CaseId);
        Assert.Equal(receipt.Id, link.ReceiptId);
        Assert.Equal([harness.StagedReceiptId], harness.Enqueuer.StagedReceiptIds);
    }

    [Fact]
    public async Task StaffLinkStandsWhenTheHandOverCannotBeEnqueued()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var harness = new Harness(
            receipt.Id,
            enqueueFailure: new IntakeDependencyUnavailableException("The queue is unavailable."));

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        Assert.Single(harness.MutationStore.Links);
        Assert.Equal([harness.StagedReceiptId], harness.Enqueuer.StagedReceiptIds);
    }

    [Fact]
    public async Task StaffLinkResolvesTheReceiptsUnidentifiedItemToTheCase()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var unidentified = new ReconcileUnidentifiedDestinationsTests.Harness();
        unidentified.Receipts.Receipts[receipt.Id] = receipt;
        var item = unidentified.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        var harness = new Harness(receipt.Id, unidentified: unidentified);

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        var resolve = Assert.Single(unidentified.Resolve.Requests);
        Assert.Equal(item.Id, resolve.UnidentifiedItemId);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, resolve.TargetKind);
        Assert.Equal(caseId.ToString("N"), resolve.TargetId);
        Assert.Equal(UnidentifiedState.Resolved, Assert.Single(unidentified.Store.Items).State);
        Assert.Equal([harness.StagedReceiptId], harness.Enqueuer.StagedReceiptIds);
    }

    /// <summary>
    /// The link has committed by the time its Unidentified item is resolved,
    /// so a resolution that fails, a conflicting operation key included, never
    /// reports the link as failed; filing is still handed to the Worker.
    /// </summary>
    [Theory]
    [InlineData("conflict")]
    [InlineData("unavailable")]
    public async Task StaffLinkStandsWhenItsUnidentifiedItemCannotBeResolved(string failure)
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var unidentified = new ReconcileUnidentifiedDestinationsTests.Harness();
        unidentified.Receipts.Receipts[receipt.Id] = receipt;
        unidentified.AddOpenItem(1, UnidentifiedOrigin.Receipt(receipt.Id));
        unidentified.Resolve.Failure = failure == "conflict"
            ? new UnidentifiedOperationConflictException()
            : new IntakeDependencyUnavailableException("The Unidentified store is unavailable.");
        var harness = new Harness(receipt.Id, unidentified: unidentified);

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        Assert.Single(harness.MutationStore.Links);
        Assert.Equal(UnidentifiedState.Open, Assert.Single(unidentified.Store.Items).State);
        Assert.Equal([harness.StagedReceiptId], harness.Enqueuer.StagedReceiptIds);
    }

    [Fact]
    public async Task StaffLinkOfAReceiptWithNoStagedWorkEnqueuesNothing()
    {
        var caseId = Guid.NewGuid();
        var receipt = LinkedReceipt(Guid.NewGuid(), caseId);
        var harness = new Harness(Guid.NewGuid());

        await harness.Link.ExecuteAsync(Request(receipt, caseId));

        Assert.Single(harness.MutationStore.Links);
        Assert.Empty(harness.Enqueuer.StagedReceiptIds);
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
            Guid workReceiptId,
            Exception? enqueueFailure = null,
            ReconcileUnidentifiedDestinationsTests.Harness? unidentified = null)
        {
            StagedReceiptId = Guid.NewGuid();
            MutationStore = new RecordingMutationStore();
            Enqueuer = new RecordingEnqueuer(enqueueFailure);
            Link = new LinkIntake(
                MutationStore,
                new NoPairing(),
                TimeProvider.System,
                workStore: new WorkStore(workReceiptId, StagedReceiptId),
                workEnqueuer: Enqueuer,
                receiptQueries: unidentified?.Receipts,
                unidentifiedDestinations: unidentified?.Reconciler);
        }

        public Guid StagedReceiptId { get; }

        public RecordingMutationStore MutationStore { get; }

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

        public Task<ImageIntakePairingResult> PairRegisteredReceiptAsync(Guid receiptId, CancellationToken cancellationToken, ImageIntakeDetail? registered = null) =>
            Task.FromResult(new ImageIntakePairingResult(0, 0, 0));

        public Task<ImageIntakePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingEnqueuer(Exception? failure) : IIntakeWorkEnqueuer
    {
        public List<Guid> StagedReceiptIds { get; } = [];

        public Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
        {
            StagedReceiptIds.Add(stagedReceiptId);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
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
    }
}
