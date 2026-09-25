using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The shared retry of every Case creator. A creation that loses a concurrency
/// race (a concurrency token, a deadlock or a unique-key collision) first
/// looks for its own committed replay, then tries again, three attempts in all.
/// When the attempts run out, <c>exhausted</c> names the failure to throw;
/// returning the caught exception rethrows it unchanged.
/// </summary>
internal static class CaseAllocationRetry
{
    private const int Attempts = 3;

    // EF's non-retrying strategy wraps a deadlock two layers deep, so every
    // layer is unwrapped.
    public static bool IsRetryable(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        _ when exception.InnerException is not null => IsRetryable(exception.InnerException),
        _ => false
    };

    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> attempt,
        Func<CancellationToken, Task<T?>> findCommitted,
        Func<Exception, Exception> exhausted,
        CancellationToken cancellationToken)
        where T : class
    {
        for (var number = 1; ; number++)
        {
            try
            {
                return await attempt(cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                var committed = await findCommitted(cancellationToken);
                if (committed is not null)
                {
                    return committed;
                }

                if (number >= Attempts)
                {
                    var failure = exhausted(exception);
                    if (ReferenceEquals(failure, exception))
                    {
                        throw;
                    }

                    throw failure;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * number), cancellationToken);
            }
        }
    }
}
