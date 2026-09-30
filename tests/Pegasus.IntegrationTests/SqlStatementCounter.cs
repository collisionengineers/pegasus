using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Counts every SQL command a context sends, whatever kind, and records the
/// rows each query read. Add it to a database with
/// <c>options.AddInterceptors(counter)</c>, call <see cref="Reset"/> after the
/// fixture is seeded, and read <see cref="Count"/> or <see cref="Reads"/> after
/// the call under test. Safe when reads run inside <c>Task.WhenAll</c>.
/// </summary>
internal sealed class SqlStatementCounter : DbCommandInterceptor
{
    private readonly List<SqlRead> reads = [];
    private int count;

    public int Count => Volatile.Read(ref count);

    /// <summary>Each query's text and the rows it read, in the order the readers closed.</summary>
    public IReadOnlyList<SqlRead> Reads
    {
        get
        {
            lock (reads)
            {
                return reads.ToArray();
            }
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref count, 0);
        lock (reads)
        {
            reads.Clear();
        }
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref count);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult DataReaderDisposing(
        DbCommand command,
        DataReaderDisposingEventData eventData,
        InterceptionResult result)
    {
        // Entity Framework also counts the last read, the one that finds no
        // row, so a query read to its end read one row fewer than it counts.
        lock (reads)
        {
            reads.Add(new(command.CommandText, Math.Max(0, eventData.ReadCount - 1)));
        }
        return result;
    }
}

/// <summary>
/// One query and the rows it returned. <see cref="Rows"/> is exact for a query
/// read to its end, such as a list; a query stopped early may show one fewer.
/// </summary>
internal sealed record SqlRead(string CommandText, int Rows);
