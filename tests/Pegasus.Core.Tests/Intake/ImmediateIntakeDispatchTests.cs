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

    private sealed class RecordingStore(Guid receiptId, List<string> events) : IIntakeWorkStore
    {
        public Guid WorkItemId { get; } = Guid.NewGuid();
        public List<Guid> ClaimedReceiptIds { get; } = [];
        public List<Guid> MarkedWorkItemIds { get; } = [];
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
            return Task.CompletedTask;
        }

        public Task<IntakeStagedReceipt?> FindBySourceIdentityAsync(IntakeSourceIdentity sourceIdentity, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReceivedIntake> ReceiveAsync(IntakeStagedReceipt receipt, string operationKey, CancellationToken cancellationToken) => throw new NotSupportedException();
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
