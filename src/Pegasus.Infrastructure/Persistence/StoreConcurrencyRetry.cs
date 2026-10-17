using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one retry of a store write that loses a concurrency race: three
/// attempts, 25 ms × attempt apart. Each attempt creates its own context and
/// transaction, so a retry reads afresh and replays a key that committed in
/// the meantime. A caller that can find its own committed result passes
/// <c>findCommitted</c>, asked after each lost race. The last attempt's
/// failure surfaces unchanged.
/// </summary>
internal static class StoreConcurrencyRetry
{
    private const int Attempts = 3;

    /// <summary>
    /// The default failure set: a stale concurrency token, a deadlock (1205)
    /// or a unique-key collision (2601, 2627). EF's non-retrying strategy
    /// wraps a deadlock two layers deep, so every layer is unwrapped.
    /// </summary>
    public static bool IsRetryable(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null => IsRetryable(exception.InnerException),
        _ => false
    };

    /// <summary>
    /// A deadlock or a unique-key collision only, every layer unwrapped: the
    /// failure set of a store that does not retry a stale concurrency token.
    /// </summary>
    public static bool IsDeadlockOrKeyCollision(Exception exception) => exception switch
    {
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null => IsDeadlockOrKeyCollision(exception.InnerException),
        _ => false
    };

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> attempt,
        CancellationToken cancellationToken,
        Func<Exception, bool>? isRetryable = null,
        Func<CancellationToken, Task<T?>>? findCommitted = null)
    {
        isRetryable ??= IsRetryable;
        for (var number = 1; ; number++)
        {
            try
            {
                return await attempt(cancellationToken);
            }
            catch (Exception exception) when (isRetryable(exception))
            {
                if (findCommitted is not null && await findCommitted(cancellationToken) is { } committed)
                {
                    return committed;
                }

                if (number == Attempts)
                {
                    throw;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * number), cancellationToken);
            }
        }
    }

    /// <summary>The same retry for a write that returns nothing.</summary>
    public static Task ExecuteAsync(
        Func<CancellationToken, Task> attempt,
        CancellationToken cancellationToken) =>
        ExecuteAsync<bool>(
            async token =>
            {
                await attempt(token);
                return true;
            },
            cancellationToken);
}
