namespace Pegasus.Core.Reports;

/// <summary>The Management Reports Period choice (item H, 9 October 2026); Custom is the From and To typed.</summary>
public enum ReportPeriod
{
    Custom,
    ThisMonth,
    LastMonth,
    ThisQuarter,
    Last12Months
}

/// <summary>
/// The period a Period choice covers, in whole London months: the current
/// month or quarter runs to now, Last month is the whole previous month and
/// Last 12 months ends now. Each fits the 366-day limit.
/// </summary>
public static class ReportPeriods
{
    /// <summary>A period ends after it starts and covers at most <see cref="GetEngineerActivityReport.MaximumPeriod"/>.</summary>
    public static bool IsValid(DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        fromUtc < toUtc && toUtc - fromUtc <= GetEngineerActivityReport.MaximumPeriod;

    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) Resolve(ReportPeriod period, DateTimeOffset nowUtc)
    {
        var today = LondonCalendar.DateAt(nowUtc);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return period switch
        {
            ReportPeriod.ThisMonth => (LondonCalendar.StartOfDay(monthStart), nowUtc),
            ReportPeriod.LastMonth => (LondonCalendar.StartOfDay(monthStart.AddMonths(-1)), LondonCalendar.StartOfDay(monthStart)),
            ReportPeriod.ThisQuarter => (LondonCalendar.StartOfDay(monthStart.AddMonths(-((today.Month - 1) % 3))), nowUtc),
            ReportPeriod.Last12Months => (Last12MonthsStart(nowUtc), nowUtc),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Custom has no fixed period.")
        };
    }

    /// <summary>
    /// The same London time twelve months earlier, unless a leap day and a
    /// BST-to-GMT span make that an hour longer than the limit (#1153).
    /// </summary>
    private static DateTimeOffset Last12MonthsStart(DateTimeOffset nowUtc)
    {
        var start = LondonCalendar.ToUtc(LondonCalendar.TimeAt(nowUtc).AddMonths(-12));
        var earliest = nowUtc - GetEngineerActivityReport.MaximumPeriod;
        return start < earliest ? earliest : start;
    }

    /// <summary>The same length of time just before the period: what "Previous period" compares with (item G).</summary>
    public static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) Previous(DateTimeOffset fromUtc, DateTimeOffset toUtc) =>
        (fromUtc - (toUtc - fromUtc), fromUtc);
}
