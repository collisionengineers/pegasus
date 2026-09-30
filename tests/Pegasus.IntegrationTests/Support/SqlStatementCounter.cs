using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Counts every SQL statement a context sends, whatever its kind, and keeps
/// the text so a test can count the statements that read one table. Reads run
/// beside each other inside <c>Task.WhenAll</c>, so it is thread-safe. Hand it
/// to <see cref="IntakeWebApplicationFactory"/> as <c>commandInterceptor:</c>,
/// or to <see cref="LocalDbTestDatabase.CreateAsync"/> through
/// <c>configureDatabase: options =&gt; options.AddInterceptors(counter)</c>.
/// </summary>
internal sealed class SqlStatementCounter : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> statements = new();

    public int Count => statements.Count;

    /// <summary>The statements sent since the last reset, in the order they started.</summary>
    public IReadOnlyList<string> Statements => [.. statements];

    public void Reset() => statements.Clear();

    /// <summary>How many statements contain every one of <paramref name="fragments"/>.</summary>
    public int CountContaining(params string[] fragments) =>
        statements.Count(text => fragments.All(fragment =>
            text.Contains(fragment, StringComparison.Ordinal)));

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        statements.Enqueue(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        statements.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        statements.Enqueue(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        statements.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        statements.Enqueue(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        statements.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
