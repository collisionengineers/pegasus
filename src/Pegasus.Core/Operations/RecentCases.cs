using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Operations;

/// <summary>
/// How a Case arrived (Work Centre D8): created by hand, by the e-mail route,
/// by the Principal API, or by the Automation actor. Derived from the accepted
/// receipt's channel, and from the creating actor where no receipt started it.
/// </summary>
public enum CaseArrival
{
    Manual,
    Email,
    PrincipalApi,
    Automation
}

/// <summary>
/// One row of the Work Centre's New cases section: a Case created in the
/// window. What the Automation actor later does to a Case is its history, not
/// a new Case (operator, 8 October 2026).
/// </summary>
public sealed record RecentCaseRow(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    string Principal,
    DateTimeOffset CreatedAtUtc,
    CaseArrival Arrival);

public sealed record RecentCasesPage(
    IReadOnlyList<RecentCaseRow> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public interface IRecentCaseQueries
{
    /// <summary>Cases created at or after <paramref name="sinceUtc"/>, newest first, less those dismissed (<see cref="WorkCentreDismissalPolicy"/>).</summary>
    Task<RecentCasesPage> ListAsync(
        DateTimeOffset sinceUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

/// <summary>
/// Per-person "since you last looked" (D8): when the person last opened the
/// Work Centre, so the feed can draw its divider. Stamped on every open.
/// </summary>
public interface IWorkCentreVisitStore
{
    Task<DateTimeOffset?> GetLastSeenAsync(Guid staffId, CancellationToken cancellationToken);

    Task MarkSeenAsync(Guid staffId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken);
}

public static class RecentCasesPolicy
{
    /// <summary>Calendar days, the same fixed rule as D3 (decided 13 September: 7 days).</summary>
    public const int WindowDays = 7;

    public const int PageSize = 50;

    /// <summary>The start of the window: midnight Europe/London, seven days back from the office's today.</summary>
    public static DateTimeOffset WindowStart(DateTimeOffset nowUtc) =>
        LondonCalendar.StartOfDay(LondonCalendar.DateAt(nowUtc).AddDays(-WindowDays));

    public static CaseArrival Arrival(IntakeSourceChannel? channel, bool createdByAutomation) => channel switch
    {
        IntakeSourceChannel.Mailbox => CaseArrival.Email,
        IntakeSourceChannel.PrincipalApi => CaseArrival.PrincipalApi,
        IntakeSourceChannel.Automation => CaseArrival.Automation,
        IntakeSourceChannel.ManualUpload => CaseArrival.Manual,
        _ => createdByAutomation ? CaseArrival.Automation : CaseArrival.Manual
    };
}

/// <summary>The New cases section as the person sees it: the page and where their "since you last looked" line falls.</summary>
public sealed record RecentCasesFeed(
    RecentCasesPage Page,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset? LastSeenUtc);

public interface IListRecentCases
{
    /// <summary>
    /// Reads the feed and, when <paramref name="markSeen"/> (the first page of an
    /// open), records this open as the person's last look so the divider on the
    /// next open moves to now. The divider returned is the previous look.
    /// </summary>
    Task<RecentCasesFeed> ExecuteAsync(ActionActor actor, int page, bool markSeen,
        CancellationToken cancellationToken, DateTimeOffset? asOfUtc = null);
}

public sealed class ListRecentCases(
    IRecentCaseQueries queries,
    IWorkCentreVisitStore visits,
    TimeProvider timeProvider) : IListRecentCases
{
    private readonly IRecentCaseQueries _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    private readonly IWorkCentreVisitStore _visits = visits ?? throw new ArgumentNullException(nameof(visits));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<RecentCasesFeed> ExecuteAsync(ActionActor actor, int page, bool markSeen,
        CancellationToken cancellationToken, DateTimeOffset? asOfUtc = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        // The Work Centre is a staff page; the Automation actor has no last look.
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "The page must be positive.");
        }

        var now = asOfUtc ?? _timeProvider.GetUtcNow();
        var since = RecentCasesPolicy.WindowStart(now);
        // The feed and the previous look share nothing, so they read together;
        // this open is stamped only after the previous look is in hand.
        var resultTask = _queries.ListAsync(since, page, RecentCasesPolicy.PageSize, cancellationToken);
        var staffId = Notifications.StaffNotificationPolicy.StaffId(actor);
        var lastSeenTask = staffId is { } id
            ? _visits.GetLastSeenAsync(id, cancellationToken)
            : Task.FromResult<DateTimeOffset?>(null);
        await Task.WhenAll(resultTask, lastSeenTask);
        if (staffId is { } stamped && markSeen)
        {
            await _visits.MarkSeenAsync(stamped, now, cancellationToken);
        }

        return new RecentCasesFeed(resultTask.Result, since, lastSeenTask.Result);
    }
}
