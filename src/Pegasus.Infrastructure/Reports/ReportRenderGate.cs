using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// One bounded process-wide admission gate for every QuestPDF render.
/// </summary>
/// <remarks>
/// A render is admitted until it has finished, not until its caller stops
/// waiting: a render that outlives its caller's budget is cancelled through
/// the token it was given, keeps counting against admission and keeps the
/// slot until it has actually stopped, so orphaned renders can never add to
/// the memory the queue may hold. A queued render holds only its openers, not
/// image bytes, so a full queue costs almost nothing.
/// </remarks>
internal sealed class ReportRenderGate : IDisposable
{
    private const int MaximumQueuedRenders = 8;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private int admitted;

    /// <summary>
    /// The renders admitted and not yet finished: the one running, including
    /// one whose caller has given up, and those waiting for it.
    /// </summary>
    internal int InFlight => Volatile.Read(ref admitted);

    /// <summary>
    /// Runs <paramref name="render"/> on its own thread once the slot is free.
    /// The token it receives is cancelled when the caller cancels or the
    /// render budget passes; the render checks it between images and stops.
    /// </summary>
    public async Task<byte[]> RunAsync(
        Func<CancellationToken, Task<byte[]>> render, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (Interlocked.Increment(ref admitted) > MaximumQueuedRenders + 1)
        {
            Interlocked.Decrement(ref admitted);
            throw new ReportRenderRejectedException("The document renderer is busy.");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(AssessmentReportRenderPolicy.RenderTimeout);
        // The token outlives this method: an orphaned render still reads it
        // after the budget source below has been disposed.
        var token = budget.Token;
        try
        {
            await semaphore.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Decrement(ref admitted);
            throw new ReportRenderRejectedException("The document renderer timed out.");
        }
        catch
        {
            Interlocked.Decrement(ref admitted);
            throw;
        }

        var task = Task.Run(async () =>
        {
            try
            {
                return await render(token).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
                Interlocked.Decrement(ref admitted);
            }
        }, CancellationToken.None);
        // A render whose caller has stopped waiting still ends, cancelled or
        // failed; its outcome is observed so it is never left unobserved.
        _ = task.ContinueWith(
            static finished => finished.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        try
        {
            return await task.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller's budget passed. The render has been told to stop and
            // keeps its slot, and its place in admission, until it does.
            throw new ReportRenderRejectedException("The document renderer timed out.");
        }
    }

    public void Dispose() => semaphore.Dispose();
}
