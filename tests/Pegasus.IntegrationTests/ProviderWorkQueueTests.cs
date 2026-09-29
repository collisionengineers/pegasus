using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Web.Background;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Web host's queue of provider work: a record is busy from its
/// reservation until its work has run, work for one record runs in turn, and
/// nothing offered is silently dropped.
/// </summary>
public sealed class ProviderWorkQueueTests
{
    private static readonly string[] FirstStartedOnly = ["first started"];
    private static readonly string[] InTurn = ["first started", "first finished", "second"];

    [Fact]
    public void AReservedRecordIsBusyUntilItsReservationIsReleased()
    {
        var queue = new ProviderWorkQueue();
        var key = Guid.NewGuid();

        using (queue.Reserve(key))
        {
            Assert.True(queue.IsInFlight(key));
        }

        Assert.False(queue.IsInFlight(key));
    }

    /// <summary>
    /// Two callers race for one record: the one whose work is queued keeps the
    /// record busy after the other lets go, so the other waits on that work
    /// instead of settling the record as interrupted.
    /// </summary>
    [Fact]
    public void TheLoserOfARaceStillSeesTheRecordBusyWhileTheWinnersWorkIsQueued()
    {
        var queue = new ProviderWorkQueue();
        var key = Guid.NewGuid();
        using var winner = queue.Reserve(key);
        var loser = queue.Reserve(key);

        Assert.Equal(ProviderWorkAdmission.Queued, winner.Admit(Work(key, (_, _) => Task.CompletedTask)));
        loser.Dispose();
        winner.Dispose();

        Assert.True(queue.IsInFlight(key));
    }

    /// <summary>
    /// A full queue queues nothing and says so; the reservation stays with the
    /// caller, who runs the work itself.
    /// </summary>
    [Fact]
    public async Task AFullQueueSaysSoAndTheCallerRunsTheWorkItself()
    {
        var queue = new ProviderWorkQueue();
        for (var queued = 0; queued < ProviderWorkQueue.Capacity; queued++)
        {
            var key = Guid.NewGuid();
            Assert.Equal(ProviderWorkAdmission.Queued, queue.Reserve(key).Admit(Work(key, (_, _) => Task.CompletedTask)));
        }
        var overflow = Guid.NewGuid();
        var ran = false;
        var work = Work(overflow, (_, _) => { ran = true; return Task.CompletedTask; });
        using var services = new ServiceCollection().BuildServiceProvider();

        using (var reservation = queue.Reserve(overflow))
        {
            Assert.Equal(ProviderWorkAdmission.Full, reservation.Admit(work));
            Assert.True(queue.IsInFlight(overflow));
            await reservation.RunHereAsync(work, services, CancellationToken.None);
        }

        Assert.True(ran);
        Assert.False(queue.IsInFlight(overflow));
    }

    /// <summary>
    /// A second piece of work for a record that already has work running is
    /// queued behind it, not dropped, and the record stays busy until both
    /// have run.
    /// </summary>
    [Fact]
    public async Task WorkForOneRecordRunsInTurnAndNothingOfferedIsDropped()
    {
        var queue = new ProviderWorkQueue();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var service = new ProviderWorkService(
            queue,
            services.GetRequiredService<IServiceScopeFactory>(),
            TimeProvider.System,
            NullLogger<ProviderWorkService>.Instance);
        var key = Guid.NewGuid();
        var order = new ConcurrentQueue<string>();
        var firstRunning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await service.StartAsync(CancellationToken.None);
        try
        {
            using (var first = queue.Reserve(key))
            {
                Assert.Equal(ProviderWorkAdmission.Queued, first.Admit(Work(key, async (_, _) =>
                {
                    order.Enqueue("first started");
                    firstRunning.SetResult();
                    await releaseFirst.Task;
                    order.Enqueue("first finished");
                })));
            }
            await firstRunning.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using (var second = queue.Reserve(key))
            {
                Assert.Equal(ProviderWorkAdmission.Queued, second.Admit(Work(key, (_, _) =>
                {
                    order.Enqueue("second");
                    return Task.CompletedTask;
                })));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
            Assert.Equal(FirstStartedOnly, order);
            Assert.True(queue.IsInFlight(key));

            releaseFirst.SetResult();
            for (var attempt = 0; queue.IsInFlight(key); attempt++)
            {
                Assert.True(attempt < 100, "The queued work did not finish.");
                await Task.Delay(TimeSpan.FromMilliseconds(50));
            }

            Assert.Equal(InTurn, order);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static ProviderWork Work(Guid key, Func<IServiceProvider, CancellationToken, Task> run) =>
        new(key, "Test", run);
}
