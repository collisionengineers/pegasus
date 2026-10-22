using System.Data.Common;

namespace Pegasus.Web;

/// <summary>
/// The one test for a cancellation: a request whose caller hung up is not an
/// application error, so it is neither logged as one nor counted by the
/// exceptions alert. SqlClient reports a command cancelled by the request token
/// in its own wording, not as a cancellation: an
/// <see cref="InvalidOperationException"/> from a cancelled read, or a
/// <see cref="DbException"/> (a <c>SqlException</c>) whose message carries that
/// wording. Only the exception itself is asked, never an inner one.
/// </summary>
internal static class ClientAbort
{
    private const string CancelledByUser = "Operation cancelled by user";

    public static bool IsCancellation(Exception exception) => exception switch
    {
        OperationCanceledException => true,
        InvalidOperationException or DbException =>
            exception.Message.Contains(CancelledByUser, StringComparison.Ordinal),
        _ => false
    };
}
