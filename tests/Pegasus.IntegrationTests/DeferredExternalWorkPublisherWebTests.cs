using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Custody;
using Pegasus.Web;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Web host publishes committed external work after the response (R315):
/// nothing is claimed, queued or marked while the handler runs, the same work is
/// then claimed, queued and marked once and in order, and a failed publication
/// is logged for the recovery sweep rather than reaching the page.
/// </summary>
public sealed class DeferredExternalWorkPublisherWebTests
{
    [Fact]
    public async Task NothingIsPublishedUntilTheResponseCompletesThenEachItemIsPublishedOnceInOrder()
    {
        var store = new RecordingStore();
        var queue = new RecordingQueue();
        var (publisher, response, _) = Compose(store, queue);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await publisher.PublishAsync(first, CancellationToken.None);
        await publisher.PublishAsync(second, CancellationToken.None);

        Assert.Empty(store.Claimed);
        Assert.Empty(queue.Enqueued);
        Assert.Equal(1, response.Registered);

        await response.CompleteAsync();

        Assert.Equal([first, second], store.Claimed);
        Assert.Equal([first, second], queue.Enqueued);
        Assert.Equal([first, second], store.Marked);
    }

    [Fact]
    public async Task AFailedPublicationIsLoggedAndTheNextItemIsStillPublished()
    {
        var store = new RecordingStore { FailClaimFor = Guid.NewGuid() };
        var queue = new RecordingQueue();
        var (publisher, response, logger) = Compose(store, queue);
        var next = Guid.NewGuid();

        await publisher.PublishAsync(store.FailClaimFor, CancellationToken.None);
        await publisher.PublishAsync(next, CancellationToken.None);
        await response.CompleteAsync();

        Assert.Equal([next], queue.Enqueued);
        Assert.Equal([next], store.Marked);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning);
    }

    [Fact]
    public async Task OutsideARequestTheWorkIsPublishedImmediately()
    {
        var store = new RecordingStore();
        var queue = new RecordingQueue();
        var accessor = new HttpContextAccessor();
        var publisher = new DeferredExternalWorkPublisher(
            new DispatchPendingExternalWork(store, queue, TimeProvider.System),
            accessor,
            new RecordingLogger());
        var work = Guid.NewGuid();

        await publisher.PublishAsync(work, CancellationToken.None);

        Assert.Equal([work], queue.Enqueued);
        Assert.Equal([work], store.Marked);
    }

    private static (DeferredExternalWorkPublisher Publisher, CapturingResponseFeature Response, RecordingLogger Logger)
        Compose(RecordingStore store, RecordingQueue queue)
    {
        var response = new CapturingResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(response);
        var logger = new RecordingLogger();
        var publisher = new DeferredExternalWorkPublisher(
            new DispatchPendingExternalWork(store, queue, TimeProvider.System),
            new HttpContextAccessor { HttpContext = context },
            logger);
        return (publisher, response, logger);
    }

    private sealed class CapturingResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> callbacks = [];

        public int Registered => callbacks.Count;

        public override void OnCompleted(Func<object, Task> callback, object state) =>
            callbacks.Add((callback, state));

        public async Task CompleteAsync()
        {
            foreach (var (callback, state) in callbacks)
            {
                await callback(state);
            }
        }
    }

    private sealed class RecordingQueue : IExternalWorkEnqueuer
    {
        public List<Guid> Enqueued { get; } = [];

        public Task EnqueueAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            Enqueued.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingStore : IExternalWorkStore
    {
        public Guid FailClaimFor { get; init; }

        public List<Guid> Claimed { get; } = [];

        public List<Guid> Marked { get; } = [];

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            Guid workItemId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            if (workItemId == FailClaimFor)
            {
                throw new InvalidOperationException("The store is unavailable.");
            }

            Claimed.Add(workItemId);
            return Task.FromResult<ExternalWorkDispatchClaim?>(new(workItemId, "lease"));
        }

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dispatchedAtUtc,
            CancellationToken cancellationToken)
        {
            Marked.Add(workItemId);
            return Task.CompletedTask;
        }

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task MarkPoisonedAsync(
            Guid workItemId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HoldsProcessingLeaseAsync(
            Guid workItemId,
            string leaseToken,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task FailProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset failedAtUtc,
            string failureCode,
            string failureReason,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeferProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingLogger : ILogger<DeferredExternalWorkPublisher>
    {
        public List<LogLevel> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(logLevel);
    }
}
