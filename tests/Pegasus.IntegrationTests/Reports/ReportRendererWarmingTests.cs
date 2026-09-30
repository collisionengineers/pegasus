using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The report renderer's first-use cost is paid by the warm-up, not by a staff
/// request. The warming render goes through the same admission gate as a real
/// one, so a real render that arrives meanwhile waits for it and it can never
/// run beside another render.
/// </summary>
public sealed class ReportRendererWarmingTests
{
    /// <summary>
    /// The warm-up leaves the renderer started: the report's fonts are
    /// registered. They are registered once per process, so this is decisive
    /// when the class runs alone, before any other render.
    /// </summary>
    [Fact]
    public async Task WarmingRendersAPageWithoutACaseAndLeavesTheGateFree()
    {
        await using var provider = RendererProvider();
        var warmer = provider.GetRequiredService<IWarmReportRenderer>();
        var gate = provider.GetRequiredService<ReportRenderGate>();

        await warmer.WarmAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(ReportResources.FontsRegistered);
        await SettledAsync(gate);
        Assert.Equal(0, gate.InFlight);
    }

    [Fact]
    public async Task WarmingWaitsForARenderThatHoldsTheGateAndCountsAgainstIt()
    {
        await using var provider = RendererProvider();
        var warmer = provider.GetRequiredService<IWarmReportRenderer>();
        var gate = provider.GetRequiredService<ReportRenderGate>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var occupying = gate.RunAsync(async _ =>
        {
            started.SetResult();
            await release.Task;
            return new byte[] { 1 };
        }, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var warming = warmer.WarmAsync();

        // Admitted beside the render that holds the slot, and not run yet.
        Assert.Equal(2, gate.InFlight);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.False(warming.IsCompleted);

        release.SetResult();
        await warming.WaitAsync(TimeSpan.FromSeconds(60));
        await occupying;
        await SettledAsync(gate);
        Assert.Equal(0, gate.InFlight);
    }

    [Fact]
    public async Task WarmingStopsWhenItsCallerCancels()
    {
        await using var provider = RendererProvider();
        var warmer = provider.GetRequiredService<IWarmReportRenderer>();
        var gate = provider.GetRequiredService<ReportRenderGate>();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => warmer.WarmAsync(cancelled.Token));

        await SettledAsync(gate);
        Assert.Equal(0, gate.InFlight);
    }

    /// <summary>
    /// A render that never ends holds the slot, not the warm-up: when the
    /// keep-warm step's bound passes or the host stops, the warm-up stops
    /// waiting at once and leaves admission, and no render is left behind it.
    /// </summary>
    [Fact]
    public async Task WarmingBehindARenderThatNeverEndsStopsWaitingWhenItsCallerCancels()
    {
        await using var provider = RendererProvider();
        var warmer = provider.GetRequiredService<IWarmReportRenderer>();
        var gate = provider.GetRequiredService<ReportRenderGate>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // It ignores its token, as QuestPDF does inside a render.
        var stuck = gate.RunAsync(async _ =>
        {
            started.SetResult();
            await release.Task;
            return new byte[] { 1 };
        }, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        using var caller = new CancellationTokenSource();
        var warming = warmer.WarmAsync(caller.Token);
        Assert.Equal(2, gate.InFlight);

        await caller.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => warming.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, gate.InFlight);
        release.SetResult();
        await stuck;
        await SettledAsync(gate);
        Assert.Equal(0, gate.InFlight);
    }

    private static async Task SettledAsync(ReportRenderGate gate)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (gate.InFlight > 0 && DateTime.UtcNow < until)
        {
            await Task.Delay(20);
        }
    }

    private static ServiceProvider RendererProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusReportRendering();
        return services.BuildServiceProvider();
    }
}
