using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Counts every SQL command a context sends - reader, non-query and scalar - so
/// a statement-count test can pin what one page or one save costs. Registered
/// through <see cref="IntakeWebApplicationFactory"/>'s <c>commandInterceptor</c>,
/// it sees the request pipeline's own contexts. Thread-safe: a page's reads run
/// side by side inside <c>Task.WhenAll</c>. Reset it after seeding and before the
/// request that is measured. It also keeps each command's text, so a test can
/// pin that one table is read once rather than only how many commands ran.
/// </summary>
internal sealed class CommandCountingInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> texts = new();

    public int Count => texts.Count;

    public void Reset() => texts.Clear();

    /// <summary>How many commands mention <paramref name="fragment"/>, such as a bracketed table name.</summary>
    public int CountMentioning(string fragment) =>
        texts.Count(text => text.Contains(fragment, StringComparison.Ordinal));

    /// <summary>Every command's text, for a failure message.</summary>
    public string Describe() => string.Join(
        Environment.NewLine,
        texts.Select((text, index) =>
        {
            var flat = text.ReplaceLineEndings(" ");
            return $"{index + 1}. {flat[..Math.Min(flat.Length, 160)]}";
        }));

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        texts.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        texts.Enqueue(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        texts.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        texts.Enqueue(command.CommandText);
        return ValueTask.FromResult(result);
    }
}
