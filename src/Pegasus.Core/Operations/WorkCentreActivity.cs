using Pegasus.Core.Actors;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>
/// The Work Centre's activity figures (FRD-15, operator 5 October 2026): what
/// the office took in and sent out today and this week. Each is a count of
/// recorded facts; none is a workflow state.
/// </summary>
/// <param name="NewCasesToday">Cases created today, excluding Triage Cases, as the New cases list counts them.</param>
/// <param name="SentToEngineerToday">First sent to Engineer events today: the once-per-Case handoff proxy (FRD-07), never a re-export.</param>
/// <param name="SentToEngineerThisWeek">The same events since Monday.</param>
/// <param name="ReportsSentToday">Sent report e-mails today, as the Engineer activity report (MI-01) counts them.</param>
/// <param name="ReportsSentThisWeek">The same since Monday.</param>
/// <param name="CompletedThisWeek">Cases that entered Complete since Monday, including one reopened since.</param>
/// <param name="EmailsReceivedToday">Mailbox receipts today; an upload is also a receipt and is not counted.</param>
public sealed record WorkCentreActivityCounts(
    int NewCasesToday,
    int SentToEngineerToday,
    int SentToEngineerThisWeek,
    int ReportsSentToday,
    int ReportsSentThisWeek,
    int CompletedThisWeek,
    int EmailsReceivedToday);

/// <summary>One read of the figures and the windows they cover.</summary>
public sealed record WorkCentreActivity(
    WorkCentreActivityCounts Counts,
    DateTimeOffset DayStartUtc,
    DateTimeOffset WeekStartUtc,
    DateTimeOffset AsOfUtc);

/// <summary>The store's aggregate read; a failed read throws, it never answers zero.</summary>
public interface IWorkCentreActivityQueries
{
    Task<WorkCentreActivityCounts> GetAsync(
        DateTimeOffset dayStartUtc,
        DateTimeOffset weekStartUtc,
        CancellationToken cancellationToken);
}

public interface IGetWorkCentreActivity
{
    Task<WorkCentreActivity> ExecuteAsync(ActionActor actor, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
}

/// <summary>
/// The windows (items D and G): Today runs from midnight Europe/London and
/// This week from Monday 00:00 Europe/London, both to the instant of the read.
/// </summary>
public static class WorkCentreActivityPolicy
{
    public static (DateTimeOffset DayStartUtc, DateTimeOffset WeekStartUtc) WindowsAt(DateTimeOffset asOfUtc)
    {
        var (dayStartUtc, _, weekStartUtc) = LondonCalendar.DayAndWeekBoundariesAt(asOfUtc);
        return (dayStartUtc, weekStartUtc);
    }
}

public sealed class GetWorkCentreActivity(IWorkCentreActivityQueries queries) : IGetWorkCentreActivity
{
    private readonly IWorkCentreActivityQueries queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<WorkCentreActivity> ExecuteAsync(
        ActionActor actor,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);
        var (dayStartUtc, weekStartUtc) = WorkCentreActivityPolicy.WindowsAt(asOfUtc);
        var counts = await queries.GetAsync(dayStartUtc, weekStartUtc, cancellationToken);
        return new WorkCentreActivity(counts, dayStartUtc, weekStartUtc, asOfUtc);
    }
}
