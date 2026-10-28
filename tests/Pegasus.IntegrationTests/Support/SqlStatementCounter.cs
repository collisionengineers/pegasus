using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The one statement counter the tests share. It counts every SQL statement a
/// context sends, whatever its kind, keeps the text so a test can count the
/// statements that read one table, keeps the reader statements apart
/// (<see cref="ReaderCount"/>, <see cref="ReaderStatements"/>), and records
/// the rows each query read (<see cref="Reads"/>). Reads run beside each other
/// inside <c>Task.WhenAll</c>, so it is thread-safe. Hand it to
/// <see cref="IntakeWebApplicationFactory"/> as <c>commandInterceptor:</c>, or
/// to <see cref="LocalDbTestDatabase.CreateAsync"/> through
/// <c>configureDatabase: options =&gt; options.AddInterceptors(counter)</c>.
/// Reset it after seeding and before the request that is measured. A test
/// asserts a ceiling or a no-growth pair on <see cref="Count"/>, never an
/// exact total (engineering, Simplicity); a zero-or-one pin on one named
/// statement stays exact.
/// </summary>
internal sealed class SqlStatementCounter : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> statements = new();
    private readonly ConcurrentQueue<string> readers = new();
    private readonly ConcurrentQueue<SqlRead> reads = new();

    public int Count => statements.Count;

    /// <summary>How many of the statements were queries (reader commands).</summary>
    public int ReaderCount => readers.Count;

    /// <summary>The statements sent since the last reset, in the order they started.</summary>
    public IReadOnlyList<string> Statements => [.. statements];

    /// <summary>The query statements alone, in the order they started.</summary>
    public IReadOnlyList<string> ReaderStatements => [.. readers];

    /// <summary>Each query's text and the rows it read, in the order the readers closed.</summary>
    public IReadOnlyList<SqlRead> Reads => [.. reads];

    public void Reset()
    {
        statements.Clear();
        readers.Clear();
        reads.Clear();
    }

    /// <summary>How many statements contain every one of <paramref name="fragments"/>.</summary>
    public int CountContaining(params string[] fragments) =>
        statements.Count(text => fragments.All(fragment =>
            text.Contains(fragment, StringComparison.Ordinal)));

    /// <summary>
    /// How many statements mention <paramref name="fragment"/>, such as a bracketed table name, and
    /// do not mention <paramref name="without"/>.
    /// </summary>
    public int CountMentioning(string fragment, string? without = null) =>
        statements.Count(text => text.Contains(fragment, StringComparison.Ordinal)
            && (without is null || !text.Contains(without, StringComparison.Ordinal)));

    /// <summary>How many statements satisfy <paramref name="predicate"/>, for a pin on one statement's shape.</summary>
    public int CountMatching(Func<string, bool> predicate) => statements.Count(predicate);

    /// <summary>
    /// How many whole-receipt reads were sent: a whole-receipt read is a
    /// receipt statement and an Assets statement, so the receipt statements
    /// that name no Assets table are counted.
    /// </summary>
    public int WholeReceiptReads() =>
        CountContaining("[IntakeReceipts]", "[InstructionDrafts]", "[IntakeMailRouteDecisions]")
        - CountContaining("[IntakeReceipts]", "[IntakeAssets]", "[InstructionDrafts]", "[IntakeMailRouteDecisions]");

    /// <summary>Every statement's text, numbered and flattened, for a failure message.</summary>
    public string Describe() => string.Join(
        Environment.NewLine,
        statements.Select((text, index) =>
        {
            var flat = text.ReplaceLineEndings(" ");
            return $"{index + 1}. {flat[..Math.Min(flat.Length, 160)]}";
        }));

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Record(command, reader: true);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command, reader: true);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Record(command, reader: false);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command, reader: false);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Record(command, reader: false);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command, reader: false);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult DataReaderDisposing(
        DbCommand command,
        DataReaderDisposingEventData eventData,
        InterceptionResult result)
    {
        // Entity Framework also counts the last read, the one that finds no
        // row, so a query read to its end read one row fewer than it counts.
        reads.Enqueue(new(command.CommandText, Math.Max(0, eventData.ReadCount - 1)));
        return result;
    }

    private void Record(DbCommand command, bool reader)
    {
        statements.Enqueue(command.CommandText);
        if (reader)
        {
            readers.Enqueue(command.CommandText);
        }
    }
}

/// <summary>
/// One query and the rows it returned. <see cref="Rows"/> is exact for a query
/// read to its end, such as a list; a query stopped early may show one fewer.
/// </summary>
internal sealed record SqlRead(string CommandText, int Rows);
