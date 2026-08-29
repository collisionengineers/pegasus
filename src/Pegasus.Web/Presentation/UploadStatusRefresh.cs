using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// How long a bounded upload-status page waits before reloading itself — the
/// one owner of that cadence, read by both the single-file and the grouped
/// status page and carried to the shared <c>data-auto-refresh</c> script.
/// </summary>
/// <remarks>
/// Work whose next attempt is already scheduled cannot progress before its
/// recorded due time, so the page waits for it instead of reloading every two
/// seconds for the thirty minutes to two hours a late attempt can be away.
/// The wait is still bounded at the top: the due time is what the retry
/// schedule intends, not a promise, and a page that went quiet for two hours
/// would stop reflecting anything else that happened to the receipt.
/// </remarks>
public static class UploadStatusRefresh
{
    /// <summary>
    /// The fastest the page ever reloads, and what everything that could
    /// progress at any moment waits.
    /// </summary>
    public const int MinimumMilliseconds = 2_000;

    private const int MaximumMilliseconds = 60_000;

    public static int DelayMilliseconds(QueuedIntakeStatus? status, DateTimeOffset nowUtc) =>
        status?.RetryDueAtUtc is { } dueAtUtc
            ? Math.Clamp(
                (int)Math.Ceiling((dueAtUtc - nowUtc).TotalMilliseconds),
                MinimumMilliseconds,
                MaximumMilliseconds)
            : MinimumMilliseconds;
}
