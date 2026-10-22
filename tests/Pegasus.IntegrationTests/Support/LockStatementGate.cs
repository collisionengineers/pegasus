using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Completes once <paramref name="expected"/> lock statements have been
/// sent, so a test knows its writers are waiting on a row lock it holds.
/// Only a query counts, so a lock taken through <c>ExecuteSqlAsync</c> is not
/// seen.
/// </summary>
internal sealed class LockStatementGate(int expected) : DbCommandInterceptor
{
    private int sent;

    public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("UPDLOCK", StringComparison.Ordinal)
            && Interlocked.Increment(ref sent) == expected)
        {
            Reached.TrySetResult();
        }

        return ValueTask.FromResult(result);
    }
}
