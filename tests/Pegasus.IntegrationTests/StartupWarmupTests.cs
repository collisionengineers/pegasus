using System.Data.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Web.Health;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The startup warm-up is best effort: nothing it meets may end the host,
/// and readiness is always released.
/// </summary>
public sealed class StartupWarmupTests
{
    [Fact]
    public async Task FailingReadsNeitherStopTheHostNorHoldReadiness()
    {
        var services = new ServiceCollection();
        // No context factory is registered, so the model step fails; the Work
        // Centre step meets a cancelled database command's error, and the
        // recent-cases step an arbitrary one.
        services.AddScoped<IGetOperationsSnapshot>(_ => throw new CancelledCommandException());
        services.AddScoped<IListRecentCases>(_ => throw new InvalidOperationException("Unreadable."));
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        Assert.False(state.IsReady);
        await warmup.StartAsync(CancellationToken.None);
        var run = Assert.IsAssignableFrom<Task>(warmup.ExecuteTask);
        await run.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(run.IsCompletedSuccessfully);
        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheKeyRingLoadsInTheWarmUpNotBeforeThePortBinds()
    {
        var keyRing = new CountingKeyRing();
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(keyRing);
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(1, keyRing.Loads);
        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheWarmUpWaitsForTheOAuthCertificatesAndThenEnds()
    {
        var store = new OAuthCertificateStore();
        var services = new ServiceCollection();
        services.AddSingleton(store);
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        var run = Assert.IsAssignableFrom<Task>(warmup.ExecuteTask);
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.False(run.IsCompleted);
        Assert.False(state.IsReady);

        store.Complete(new OAuthCertificateSet([], []));
        await run.WaitAsync(TimeSpan.FromMinutes(1));

        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheWarmUpRunsTheMailWebhookSubscriptionRead()
    {
        var subscriptions = new CountingSubscriptionStore();
        var services = new ServiceCollection();
        services.AddSingleton<IApprovedMailboxSubscriptionStore>(subscriptions);
        await using var provider = services.BuildServiceProvider();
        var state = new StartupWarmupState(warms: true);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            NullLogger<StartupWarmup>.Instance);

        await warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));

        Assert.Equal(1, subscriptions.ActiveReads);
        Assert.True(state.IsReady);
        await warmup.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheModelStepLineCarriesTheModelBuildAndTheFirstConnectionTimings()
    {
        // A connection that is refused at once: the model is real, the login
        // never happens, and CanConnectAsync answers false in about a second.
        var services = new ServiceCollection();
        services.AddDbContextFactory<PegasusDbContext>(options => options.UseSqlServer(
            "Server=tcp:127.0.0.1,1;Database=WarmupTimings;Connect Timeout=2;ConnectRetryCount=0;Encrypt=False"));
        await using var provider = services.BuildServiceProvider();
        var logger = new RecordingLogger();
        var state = new StartupWarmupState(warms: true);
        using var warmup = new StartupWarmup(
            provider.GetRequiredService<IServiceScopeFactory>(),
            state,
            TimeProvider.System,
            logger);

        await warmup.StartAsync(CancellationToken.None);
        await Assert.IsAssignableFrom<Task>(warmup.ExecuteTask).WaitAsync(TimeSpan.FromMinutes(1));
        await warmup.StopAsync(CancellationToken.None);

        var line = Assert.Single(
            logger.Messages,
            message => message.StartsWith("Startup warm-up step model finished in ", StringComparison.Ordinal));
        Assert.Matches(@" ms \(model build \d+ ms, first connection \d+ ms\)$", line);
    }

    [Fact]
    public void AHostThatDoesNotWarmIsReadyAtOnce()
    {
        Assert.True(new StartupWarmupState(warms: false).IsReady);
    }

    private sealed class RecordingLogger : ILogger<StartupWarmup>
    {
        private readonly object gate = new();
        private readonly List<string> messages = [];

        public string[] Messages
        {
            get
            {
                lock (gate)
                {
                    return messages.ToArray();
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
            lock (gate)
            {
                messages.Add(formatter(state, exception));
            }
        }
    }

    private sealed class CountingKeyRing : IDataProtectionProvider
    {
        private int loads;

        public int Loads => Volatile.Read(ref loads);

        public IDataProtector CreateProtector(string purpose)
        {
            Interlocked.Increment(ref loads);
            return new EphemeralDataProtectionProvider().CreateProtector(purpose);
        }
    }

    private sealed class CountingSubscriptionStore : IApprovedMailboxSubscriptionStore
    {
        private int activeReads;

        public int ActiveReads => Volatile.Read(ref activeReads);

        public Task<ApprovedMailboxSubscription?> GetActiveAsync(string subscriptionId,
            DateTimeOffset nowUtc, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref activeReads);
            return Task.FromResult<ApprovedMailboxSubscription?>(null);
        }

        public Task<IReadOnlyList<ApprovedMailboxSubscription>> ListAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>
            ListMaintenanceCandidatesAsync(DateTimeOffset nowUtc,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(ApprovedMailboxSubscription value,
            string? expectedPriorSubscriptionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordMaintenanceFailureAsync(Guid approvedMailboxId, long expectedGeneration,
            string? expectedSubscriptionId, string failureCode,
            DateTimeOffset attemptedAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    /// <summary>What SQL Server raises when a command is cancelled mid-flight.</summary>
    private sealed class CancelledCommandException() : DbException("Operation cancelled by user.");
}
