namespace Pegasus.Web.Presentation;

/// <summary>
/// One request's independent reads, run side by side but never more than
/// <see cref="Limit"/> at once, so a page cannot flood the database.
/// </summary>
/// <remarks>
/// Only a read that opens its own database context from the context factory,
/// or touches no database, may be started here. The request's scoped context
/// is not safe for concurrent use, so a read on it stays sequential. A read
/// returns its result; the caller assigns page state only after
/// <see cref="WhenAllAsync"/>, never from inside a read.
/// </remarks>
internal sealed class BoundedReads : IDisposable
{
    public const int Limit = 4;

    private readonly SemaphoreSlim slots = new(Limit, Limit);
    private readonly List<Task> started = [];
    private readonly CancellationToken cancellationToken;

    public BoundedReads(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
    }

    /// <summary>Starts a read once a slot is free.</summary>
    public Task<T> Start<T>(Func<CancellationToken, Task<T>> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var task = RunAsync(read);
        started.Add(task);
        return task;
    }

    /// <summary>
    /// Waits for every started read. It returns only once all have finished,
    /// so none outlives the request; the first failure is then thrown.
    /// </summary>
    public Task WhenAllAsync() => Task.WhenAll(started);

    /// <summary>
    /// Releases the slots once every read has finished. A read still running
    /// (the page left early on a failure) keeps them until it ends.
    /// </summary>
    public void Dispose()
    {
        if (started.TrueForAll(task => task.IsCompleted))
        {
            slots.Dispose();
        }
    }

    private async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> read)
    {
        await slots.WaitAsync(cancellationToken);
        try
        {
            return await read(cancellationToken);
        }
        finally
        {
            slots.Release();
        }
    }
}
