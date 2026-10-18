namespace Pegasus.Core.Tests.Support;

/// <summary>
/// Counts the calls in flight at once, and can hold each call until a number of
/// them overlap. The hold gives up after thirty seconds, so a caller that never
/// overlaps its calls fails instead of hanging.
/// </summary>
internal sealed class ConcurrentCallProbe
{
    private readonly Lock sync = new();
    private readonly TaskCompletionSource allHeld =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int inFlight;

    /// <summary>Hold each call until this many are in flight at once; zero holds none.</summary>
    public int HoldUntil { get; set; }

    /// <summary>After the hold, keep each call waiting until its token is cancelled.</summary>
    public bool HoldUntilCancelled { get; set; }

    /// <summary>The most calls that were in flight together.</summary>
    public int Peak { get; private set; }

    /// <summary>Completes once <see cref="HoldUntil"/> calls are in flight together.</summary>
    public Task AllHeld => allHeld.Task;

    public async Task<T> RunAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            inFlight++;
            Peak = Math.Max(Peak, inFlight);
            if (HoldUntil > 0 && inFlight >= HoldUntil)
            {
                allHeld.TrySetResult();
            }
        }

        try
        {
            if (HoldUntil > 0)
            {
                await allHeld.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }

            if (HoldUntilCancelled)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return await call();
        }
        finally
        {
            lock (sync)
            {
                inFlight--;
            }
        }
    }
}
