namespace Pegasus.Core.Intake;

/// <summary>
/// The intake escape window: how long a grouped-image receipt may sit pending
/// before the sweep registers its group Unidentified, how long a manual
/// upload's matched Cases wait for a staff destination before the images
/// register (FRD-18), and how long after a pipeline association its filing is
/// still replayed (R377). This is the one rule that decides when it closes.
/// For a group (<see cref="HasClosed"/>) it is judged on the store's clock,
/// the SQL server's (<c>SYSUTCDATETIME()</c> in the query that read the
/// group), not the process clock, and the group's stored receipt time is its
/// start; the adapter only reads that clock (R191, R379). The Case-filing
/// replay's cutoff (<see cref="LatestClosedStart"/>) is still taken from
/// Core's <see cref="TimeProvider"/>.
/// </summary>
public static class IntakeEscapeWindow
{
    /// <summary>
    /// The window's length. Matches the longest delay in a genuine processing
    /// failure's retry budget (<c>ProcessQueuedIntake.RetryDelays</c>) rather
    /// than inventing a second bound.
    /// </summary>
    public static readonly TimeSpan Length = TimeSpan.FromHours(2);

    /// <summary>
    /// Whether the group's window has closed: closed from exactly its length
    /// after its stored receipt time, on the store's clock as the read that
    /// returned the group took it. A group no read returned (a write's
    /// result) has no such reading, so it throws rather than falling back to
    /// the process clock.
    /// </summary>
    public static bool HasClosed(IntakeSubmissionGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var nowUtc = group.ReadAtUtc ?? throw new InvalidOperationException(
            "The submission group carries no store clock reading.");
        return group.ReceivedAtUtc <= LatestClosedStart(nowUtc);
    }

    /// <summary>
    /// The latest start whose window has closed at <paramref name="nowUtc"/>:
    /// a window started at or before it has closed, one started after it is
    /// still open.
    /// </summary>
    public static DateTimeOffset LatestClosedStart(DateTimeOffset nowUtc) => nowUtc - Length;
}
