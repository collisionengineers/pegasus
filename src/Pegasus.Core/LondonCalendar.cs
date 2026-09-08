namespace Pegasus.Core;

/// <summary>
/// Converts Europe/London civil dates and times to deterministic UTC instants. Invalid local
/// times advance to the first valid minute; ambiguous local times select the later instant.
/// </summary>
public static class LondonCalendar
{
    private const string TimeZoneId = "Europe/London";

    public static DateTimeOffset StartOfDay(DateOnly date) =>
        StartOfDay(date, GetTimeZone());

    public static DateTimeOffset? StartOfNextDay(DateOnly date)
    {
        return date == DateOnly.MaxValue
            ? null
            : StartOfDay(date.AddDays(1), GetTimeZone());
    }

    public static DateOnly DateAt(DateTimeOffset instant) =>
        DateAt(instant, GetTimeZone());

    /// <summary>
    /// The start and end of the office's today and the start of its week, expressed in UTC.
    /// </summary>
    /// <remarks>
    /// The week starts on Monday, which is the week the office works to.
    /// "Today" means the office's today: counting from a UTC midnight would
    /// move the boundary by an hour for half the year and silently reassign
    /// work between days.
    /// </remarks>
    public static (
        DateTimeOffset DayStartUtc,
        DateTimeOffset DayEndUtc,
        DateTimeOffset WeekStartUtc)
        DayAndWeekBoundariesAt(DateTimeOffset instant)
    {
        var timeZone = GetTimeZone();
        var date = DateAt(instant, timeZone);
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return (
            StartOfDay(date, timeZone),
            StartOfDay(date.AddDays(1), timeZone),
            StartOfDay(date.AddDays(-daysSinceMonday), timeZone));
    }

    public static DateTimeOffset ToUtc(DateTime localTime) =>
        ToUtc(localTime, GetTimeZone());

    public static DateTime TimeAt(DateTimeOffset instant) =>
        LocalAt(instant).DateTime;

    public static DateTimeOffset LocalAt(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, GetTimeZone());

    private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo timeZone) =>
        ToUtc(date.ToDateTime(TimeOnly.MinValue), timeZone);

    private static DateOnly DateAt(DateTimeOffset instant, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);

    private static DateTimeOffset ToUtc(DateTime localTime, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = timeZone.IsAmbiguousTime(local)
            ? timeZone.GetAmbiguousTimeOffsets(local).Min()
            : timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static TimeZoneInfo GetTimeZone() =>
        TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

}
