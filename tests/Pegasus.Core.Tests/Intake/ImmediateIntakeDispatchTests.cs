using System.Diagnostics;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class ImmediateIntakeDispatchTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CommittedReceiptIsQueuedAndMarkedWithoutScanningTheOutbox()
    {
        var receiptId = Guid.NewGuid();
        var events = new List<string>();
        var store = new RecordingStore(receiptId, events);
        var queue = new RecordingQueue(events);
        var dispatcher = new DispatchPendingIntakeWork(
            store,
            queue,
            new FixedTimeProvider(FixedUtcNow));

        await dispatcher.ExecuteCommittedAsync(receiptId, CancellationToken.None);

        Assert.Equal([receiptId], store.ClaimedReceiptIds);
        Assert.Equal([receiptId], queue.EnqueuedReceiptIds);
        Assert.Equal([store.WorkItemId], store.MarkedWorkItemIds);
        Assert.Equal(["claim", "enqueue", "mark"], events);
        Assert.Empty(store.Released);
    }

    [Fact]
    public async Task ReceivingANewManualUploadPublishesItsCommittedReceiptIdentifier()
    {
        var store = new RecordingStore(Guid.NewGuid(), []);
        var publisher = new RecordingCommittedPublisher();
        var receiver = new ReceiveIntake(
            new MemoryArtifactStore(),
            store,
            new FixedTimeProvider(FixedUtcNow),
            publisher);

        var received = await receiver.ExecuteAsync(
            new IntakeSource(
                "manual.pdf",
                "application/pdf",
                new byte[] { 0x01, 0x02 },
                FixedUtcNow,
                "staff:test",
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "manual-upload-1")),
            "manual-upload-1",
            CancellationToken.None);

        Assert.False(received.IsDuplicate);
        Assert.Equal([received.StagedReceiptId], publisher.StagedReceiptIds);
        Assert.Equal([received.StagedReceiptId], store.ReceivedReceiptIds);
    }

    [Fact]
    public async Task QueueFailureLeavesCommittedReceiptDueForRecoveryWithoutThrowing()
    {
        var receiptId = Guid.NewGuid();
        var events = new List<string>();
        var store = new RecordingStore(receiptId, events);
        var queue = new RecordingQueue(events, new IOException("queue unavailable"));
        var dispatcher = new DispatchPendingIntakeWork(
            store,
            queue,
            new FixedTimeProvider(FixedUtcNow));

        await dispatcher.ExecuteCommittedAsync(receiptId, CancellationToken.None);

        Assert.Equal([receiptId], store.ClaimedReceiptIds);
        Assert.Equal([receiptId], queue.EnqueuedReceiptIds);
        Assert.Empty(store.MarkedWorkItemIds);
        Assert.Equal([(store.WorkItemId, "lease-token", FixedUtcNow)], store.Released);
        Assert.Equal(["claim", "enqueue", "release"], events);
    }

    [Fact]
    public async Task ReleaseFailureStillKeepsTheCommittedReceiptAcknowledgedForLeaseExpiryRecovery()
    {
        var receiptId = Guid.NewGuid();
        var events = new List<string>();
        var store = new RecordingStore(
            receiptId,
            events,
            releaseFailure: new IOException("database unavailable"));
        var dispatcher = new DispatchPendingIntakeWork(
            store,
            new RecordingQueue(events, new IOException("queue unavailable")),
            new FixedTimeProvider(FixedUtcNow));

        await dispatcher.ExecuteCommittedAsync(receiptId, CancellationToken.None);

        Assert.Equal([receiptId], store.ClaimedReceiptIds);
        Assert.Empty(store.MarkedWorkItemIds);
        Assert.Equal(["claim", "enqueue", "release"], events);
    }

    [Fact]
    public async Task ImmediatePublicationRecordsTheReceiptIdentifierAndBoundedOutcome()
    {
        var receiptId = Guid.NewGuid();
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Pegasus.Core.Intake",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            ActivityStopped = activity => activities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var dispatcher = new DispatchPendingIntakeWork(
            new RecordingStore(receiptId, []),
            new RecordingQueue([]),
            new FixedTimeProvider(FixedUtcNow));

        await dispatcher.ExecuteCommittedAsync(receiptId, CancellationToken.None);

        var activity = Assert.Single(activities);
        Assert.Equal("publish_committed_intake_work", activity.OperationName);
        Assert.Equal(receiptId, activity.GetTagItem("intake.staged_receipt_id"));
        Assert.Equal("published", activity.GetTagItem("intake.publication.outcome"));
    }

    private sealed class RecordingQueue(
        List<string> events,
        Exception? failure = null) : IIntakeWorkEnqueuer
    {
        public List<Guid> EnqueuedReceiptIds { get; } = [];

        public Task EnqueueAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
        {
            events.Add("enqueue");
            EnqueuedReceiptIds.Add(stagedReceiptId);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class RecordingCommittedPublisher : ICommittedIntakeWorkPublisher
    {
        public List<Guid> StagedReceiptIds { get; } = [];

        public Task PublishAsync(Guid stagedReceiptId, CancellationToken cancellationToken)
        {
            StagedReceiptIds.Add(stagedReceiptId);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryArtifactStore : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(
            string contentHash,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            Task.FromResult($"source/{contentHash}");

        public Task<ReadOnlyMemory<byte>?> ReadAsync(
            string storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(null);
    }

    private sealed class RecordingStore(
        Guid receiptId,
        List<string> events,
        Exception? releaseFailure = null) : IIntakeWorkStore
    {
        public Guid WorkItemId { get; } = Guid.NewGuid();
        public List<Guid> ClaimedReceiptIds { get; } = [];
        public List<Guid> MarkedWorkItemIds { get; } = [];
        public List<Guid> ReceivedReceiptIds { get; } = [];
        public List<(Guid WorkItemId, string LeaseToken, DateTimeOffset DueAtUtc)> Released { get; } = [];

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The committed path must not scan the outbox.");

        public Task<IntakeWorkItem?> ClaimDispatchAsync(
            Guid stagedReceiptId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            events.Add("claim");
            ClaimedReceiptIds.Add(stagedReceiptId);
            return Task.FromResult<IntakeWorkItem?>(stagedReceiptId == receiptId
                ? new IntakeWorkItem(
                    WorkItemId,
                    receiptId,
                    "operation",
                    IntakeWorkState.Dispatching,
                    0,
                    nowUtc,
                    "lease-token",
                    nowUtc.Add(leaseDuration),
                    null,
                    null)
                : null);
        }

        public Task MarkDispatchedAsync(Guid workItemId, string leaseToken, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            events.Add("mark");
            MarkedWorkItemIds.Add(workItemId);
            return Task.CompletedTask;
        }

        public Task ReleaseDispatchAsync(Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
        {
            events.Add("release");
            Released.Add((workItemId, leaseToken, dueAtUtc));
            return releaseFailure is null ? Task.CompletedTask : Task.FromException(releaseFailure);
        }

        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(IntakeSourceIdentity sourceIdentity, CancellationToken cancellationToken) => Task.FromResult<IntakeStagedReceipt?>(null);
        public Task<ReceivedIntake> ReceiveAsync(IntakeStagedReceipt receipt, string operationKey, CancellationToken cancellationToken)
        {
            ReceivedReceiptIds.Add(receipt.Id);
            return Task.FromResult(new ReceivedIntake(receipt.Id, false));
        }
        public Task<IntakeWorkItem?> FindWorkItemAsync(Guid stagedReceiptId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(IntakeWorkItem WorkItem, IntakeStagedReceipt Receipt)?> ClaimProcessingAsync(Guid stagedReceiptId, DateTimeOffset nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeEvaluationRevision> CompleteProcessingAsync(Guid workItemId, string leaseToken, Guid processedReceiptId, DateTimeOffset completedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IntakeEvaluationRevision?> GetCompletedEvaluationAsync(Guid stagedReceiptId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RetryProcessingAsync(Guid workItemId, string leaseToken, DateTimeOffset dueAtUtc, string failureCode, bool terminal, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task MarkPoisonedAsync(Guid stagedReceiptId, DateTimeOffset failedAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> RecoverInterruptedWorkAsync(DateTimeOffset nowUtc, DateTimeOffset staleDispatchedBeforeUtc, int maximumItems, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ScheduleReevaluationAsync(Guid stagedReceiptId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Guid?> FindStagedReceiptIdForReceiptAsync(Guid intakeReceiptId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
