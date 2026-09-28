using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Operations;
using Pegasus.Web.Health;

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
    public void AHostThatDoesNotWarmIsReadyAtOnce()
    {
        Assert.True(new StartupWarmupState(warms: false).IsReady);
    }

    /// <summary>What SQL Server raises when a command is cancelled mid-flight.</summary>
    private sealed class CancelledCommandException() : DbException("Operation cancelled by user.");
}
