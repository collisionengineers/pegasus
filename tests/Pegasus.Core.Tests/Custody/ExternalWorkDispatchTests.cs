using Pegasus.Core.Custody;

namespace Pegasus.Core.Tests.Custody;

public sealed class ExternalWorkDispatchTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ClaimedWorkIsQueuedBeforeItsDispatchLeaseIsCompleted()
    {
        var events = new List<string>();
        var workId = Guid.NewGuid();
        var store = new RecordingStore(
            new ExternalWorkDispatchClaim(workId, "lease-token"),
            events);
        var queue = new RecordingQueue(events);
        var dispatcher = new DispatchPendingExternalWork(
            store,
            queue,
            new FixedTimeProvider(FixedUtcNow));

        var dispatched = await dispatcher.ExecuteAsync(10, CancellationToken.None);

        Assert.Equal(1, dispatched);
        Assert.Equal([workId], queue.WorkItemIds);
        Assert.Equal(["claim", "enqueue", "mark", "claim"], events);
        Assert.Equal((workId, "lease-token", FixedUtcNow), Assert.Single(store.Marked));
        Assert.Empty(store.Released);
    }

    [Fact]
    public async Task UncertainQueueFailureReleasesTheClaimForReplay()
    {
        var events = new List<string>();
        var workId = Guid.NewGuid();
        var store = new RecordingStore(
            new ExternalWorkDispatchClaim(workId, "lease-token"),
            events);
        var queue = new RecordingQueue(events, new IOException("queue unavailable"));
        var dispatcher = new DispatchPendingExternalWork(
            store,
            queue,
            new FixedTimeProvider(FixedUtcNow));

        await Assert.ThrowsAsync<IOException>(() =>
            dispatcher.ExecuteAsync(1, CancellationToken.None));

        Assert.Empty(store.Marked);
        Assert.Equal(
            (workId, "lease-token", FixedUtcNow.AddSeconds(30)),
            Assert.Single(store.Released));
        Assert.Equal(["claim", "enqueue", "release"], events);
    }

    [Fact]
    public async Task CommittedWorkUsesItsExactIdentifierAndKeepsTheCommittedResultOnQueueFailure()
    {
        var events = new List<string>();
        var workId = Guid.NewGuid();
        var store = new RecordingStore(
            new ExternalWorkDispatchClaim(workId, "lease-token"),
            events);
        var dispatcher = new DispatchPendingExternalWork(
            store,
            new RecordingQueue(events, new IOException("queue unavailable")),
            new FixedTimeProvider(FixedUtcNow));

        await dispatcher.ExecuteCommittedAsync(workId, CancellationToken.None);

        Assert.Empty(store.Marked);
        Assert.Equal(
            (workId, "lease-token", FixedUtcNow),
            Assert.Single(store.Released));
        Assert.Equal(["claim", "enqueue", "release"], events);
    }

    private sealed class RecordingQueue(
        List<string> events,
        Exception? failure = null) : IExternalWorkEnqueuer
    {
        public List<Guid> WorkItemIds { get; } = [];

        public Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            events.Add("enqueue");
            WorkItemIds.Add(workItemId);
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
    }

    private sealed class RecordingStore(
        ExternalWorkDispatchClaim claim,
        List<string> events) : IExternalWorkStore
    {
        private bool claimed;

        public List<(Guid WorkItemId, string LeaseToken, DateTimeOffset AtUtc)> Marked { get; } = [];

        public List<(Guid WorkItemId, string LeaseToken, DateTimeOffset DueAtUtc)> Released { get; } = [];

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            events.Add("claim");
            if (claimed)
            {
                return Task.FromResult<ExternalWorkDispatchClaim?>(null);
            }

            claimed = true;
            return Task.FromResult<ExternalWorkDispatchClaim?>(claim);
        }

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            Guid workItemId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            events.Add("claim");
            if (claimed || workItemId != claim.WorkItemId)
            {
                return Task.FromResult<ExternalWorkDispatchClaim?>(null);
            }

            claimed = true;
            return Task.FromResult<ExternalWorkDispatchClaim?>(claim);
        }

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dispatchedAtUtc,
            CancellationToken cancellationToken)
        {
            events.Add("mark");
            Marked.Add((workItemId, leaseToken, dispatchedAtUtc));
            return Task.CompletedTask;
        }

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken)
        {
            events.Add("release");
            Released.Add((workItemId, leaseToken, dueAtUtc));
            return Task.CompletedTask;
        }

        public Task MarkPoisonedAsync(
            Guid workItemId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<bool> HoldsProcessingLeaseAsync(
            Guid workItemId,
            string leaseToken,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task FailProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset failedAtUtc,
            string failureCode,
            string failureReason,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
