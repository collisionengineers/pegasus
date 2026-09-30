using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pegasus.Infrastructure.Custody;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The background renewal: it starts only once the application has started,
/// checks at once and then every sixty seconds, and nothing it meets, from an
/// unresolved Box secret to a mint that keeps failing, stops the loop or the
/// host. Time is driven by hand so the sixty seconds are asserted, not waited.
/// </summary>
public sealed class BoxTokenRenewalServiceTests
{
    [Fact]
    public async Task TheFirstCheckRunsOnlyOnceTheApplicationHasStarted()
    {
        using var harness = new Harness();
        await harness.Service.StartAsync(CancellationToken.None);

        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0, harness.Provider.Calls);

        harness.Lifetime.Start();
        await Eventually(() => harness.Provider.Calls == 1);
    }

    [Fact]
    public async Task ItChecksAgainEverySixtySeconds()
    {
        using var harness = new Harness();
        await harness.StartedAsync();
        await Eventually(() => harness.Provider.Calls == 1);
        await Eventually(() => harness.Time.HasActiveTimer);

        harness.Time.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal(1, harness.Provider.Calls);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await Eventually(() => harness.Provider.Calls == 2);
        await Eventually(() => harness.Time.HasActiveTimer);
        harness.Time.Advance(BoxTokenRenewalService.CheckInterval);
        await Eventually(() => harness.Provider.Calls == 3);
    }

    /// <summary>
    /// A mint that throws is logged, at Warning for the first of a run of
    /// failures and at Debug for the rest, and the loop goes on. A later
    /// success ends the run, so the next failure is a Warning again.
    /// </summary>
    [Fact]
    public async Task AFailingMintDoesNotStopTheLoopAndOnlyTheFirstFailureOfARunWarns()
    {
        using var harness = new Harness();
        harness.Provider.Outcomes.Enqueue(new InvalidOperationException("no token"));
        harness.Provider.Outcomes.Enqueue(new InvalidOperationException("still no token"));
        harness.Provider.Outcomes.Enqueue(null);
        harness.Provider.Outcomes.Enqueue(new InvalidOperationException("no token again"));

        await harness.StartedAsync();
        for (var check = 1; check <= 4; check++)
        {
            await Eventually(() => harness.Provider.Calls == check);
            await Eventually(() => harness.Time.HasActiveTimer);
            harness.Time.Advance(BoxTokenRenewalService.CheckInterval);
        }
        await Eventually(() => harness.Provider.Calls == 5);

        Assert.Equal(
            [LogLevel.Warning, LogLevel.Debug, LogLevel.Warning],
            harness.Logger.Levels);
        Assert.False(harness.Service.ExecuteTask!.IsCompleted);
    }

    /// <summary>
    /// The Box options are built at first use so that an unresolved Key Vault
    /// secret fails a Box work item and never the host. The service must not
    /// resolve the provider until it checks, and must survive that failing.
    /// </summary>
    [Fact]
    public async Task AProviderThatCannotBeBuiltIsALoggedFailureNotACrash()
    {
        using var harness = new Harness(providerFactory: _ => throw new InvalidOperationException(
            "Box:ConfigJson is an unresolved Key Vault reference; the platform has not resolved the secret."));

        await harness.StartedAsync();
        await Eventually(() => harness.Logger.Levels.Count == 1);

        Assert.Equal([LogLevel.Warning], harness.Logger.Levels);
        Assert.False(harness.Service.ExecuteTask!.IsCompleted);
        await harness.Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ItStopsWhenTheHostStopsAndChecksNoMore()
    {
        using var harness = new Harness();
        await harness.StartedAsync();
        await Eventually(() => harness.Provider.Calls == 1);
        await Eventually(() => harness.Time.HasActiveTimer);

        await harness.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(harness.Service.ExecuteTask!.IsCompletedSuccessfully);

        harness.Time.Advance(BoxTokenRenewalService.CheckInterval);
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, harness.Provider.Calls);
    }

    [Fact]
    public async Task ItStopsCleanlyIfTheHostStopsBeforeTheApplicationStarted()
    {
        using var harness = new Harness();
        await harness.Service.StartAsync(CancellationToken.None);

        await harness.Service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(harness.Service.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Equal(0, harness.Provider.Calls);
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not reached in ten seconds.");
            await Task.Delay(5);
        }
    }

    private sealed class Harness : IDisposable
    {
        public Harness(Func<IServiceProvider, IBoxAuthorizationHeaderProvider>? providerFactory = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(providerFactory ?? (_ => Provider));
            Services = services.BuildServiceProvider();
            Service = new BoxTokenRenewalService(Services, Lifetime, Time, Logger);
        }

        public ServiceProvider Services { get; }
        public RecordingProvider Provider { get; } = new();
        public FakeLifetime Lifetime { get; } = new();
        public ManualTimeProvider Time { get; } = new();
        public RecordingLogger Logger { get; } = new();
        public BoxTokenRenewalService Service { get; }

        public async Task StartedAsync()
        {
            await Service.StartAsync(CancellationToken.None);
            Lifetime.Start();
        }

        public void Dispose()
        {
            Service.Dispose();
            Lifetime.Dispose();
            Services.Dispose();
        }
    }

    /// <summary>
    /// A provider that answers each renewal with the next queued outcome: an
    /// exception to throw, or null for a check that finds the token live.
    /// </summary>
    private sealed class RecordingProvider : IBoxAuthorizationHeaderProvider
    {
        private int calls;

        public System.Collections.Concurrent.ConcurrentQueue<Exception?> Outcomes { get; } = new();
        public int Calls => Volatile.Read(ref calls);

        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The renewal never asks for a header.");

        public Task<bool> RenewIfDueAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Outcomes.TryDequeue(out var outcome) && outcome is not null
                ? Task.FromException<bool>(outcome)
                : Task.FromResult(false);
        }
    }

    private sealed class FakeLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();

        public CancellationToken ApplicationStarted => started.Token;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void Start() => started.Cancel();

        public void StopApplication()
        {
        }

        public void Dispose() => started.Dispose();
    }

    private sealed class RecordingLogger : ILogger<BoxTokenRenewalService>
    {
        private readonly Lock guard = new();
        private readonly List<LogLevel> levels = [];

        public IReadOnlyList<LogLevel> Levels
        {
            get
            {
                lock (guard)
                {
                    return [.. levels];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (guard)
            {
                levels.Add(logLevel);
            }
        }
    }

    /// <summary>
    /// A clock that moves only when the test says so and fires the timers a
    /// <c>Task.Delay</c> made against it when their time comes.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly Lock guard = new();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset now = new(2026, 9, 30, 9, 0, 0, TimeSpan.Zero);

        public bool HasActiveTimer
        {
            get
            {
                lock (guard)
                {
                    return timers.Any(timer => timer.Due is not null);
                }
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (guard)
            {
                return now;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            lock (guard)
            {
                var timer = new ManualTimer(guard, callback, state, now + dueTime);
                timers.Add(timer);
                return timer;
            }
        }

        public void Advance(TimeSpan interval)
        {
            ManualTimer[] fire;
            lock (guard)
            {
                now += interval;
                fire = [.. timers.Where(timer => timer.Due <= now)];
                timers.RemoveAll(timer => timer.Due <= now || timer.Due is null);
            }
            foreach (var timer in fire)
            {
                timer.Fire();
            }
        }

        /// <summary>One timer. Its due time is read and cleared under the provider's lock.</summary>
        private sealed class ManualTimer(
            Lock guard,
            TimerCallback callback,
            object? state,
            DateTimeOffset initialDue) : ITimer
        {
            private DateTimeOffset? due = initialDue;

            public DateTimeOffset? Due
            {
                get
                {
                    lock (guard)
                    {
                        return due;
                    }
                }
            }

            public void Fire()
            {
                Clear();
                callback(state);
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose() => Clear();

            public ValueTask DisposeAsync()
            {
                Clear();
                return ValueTask.CompletedTask;
            }

            private void Clear()
            {
                lock (guard)
                {
                    due = null;
                }
            }
        }
    }
}
