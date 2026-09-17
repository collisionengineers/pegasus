using Pegasus.Core.Reports;

namespace Pegasus.Infrastructure.Reports;

/// <summary>One bounded process-wide admission gate for every QuestPDF render.</summary>
internal sealed class ReportRenderGate : IDisposable
{
    private const int MaximumQueuedRenders = 8;
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private int queued;

    public async Task<byte[]> RunAsync(Func<byte[]> render, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(render);
        if (Interlocked.Increment(ref queued) > MaximumQueuedRenders)
        {
            Interlocked.Decrement(ref queued);
            throw new ReportRenderRejectedException("The document renderer is busy.");
        }

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(AssessmentReportRenderPolicy.RenderTimeout);
        try
        {
            await semaphore.WaitAsync(budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReportRenderRejectedException("The document renderer timed out.");
        }
        finally
        {
            Interlocked.Decrement(ref queued);
        }

        var task = Task.Run(() =>
        {
            try
            {
                return render();
            }
            finally
            {
                semaphore.Release();
            }
        }, CancellationToken.None);
        try
        {
            return await task.WaitAsync(budget.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ReportRenderRejectedException("The document renderer timed out.");
        }
    }

    public void Dispose() => semaphore.Dispose();
}
