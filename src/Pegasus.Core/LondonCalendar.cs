namespace Pegasus.Core;

/// <summary>
/// Converts Europe/London civil dates and times to deterministic UTC instants. Invalid local
/// times advance to the first valid minute; ambiguous local times select the later instant.
/// </summary>
/// <remarks>
/// If the platform cannot resolve the Europe/London time zone, conversion falls back to UTC so
/// an unavailable time-zone database does not make office operations unavailable.
/// </remarks>
public static class LondonCalendar
{
    private const string TimeZoneId = "Europe/London";

    private static readonly TimeZoneInfo TimeZone =
        ResolveTimeZone();

    public static DateTimeOffset StartOfDay(DateOnly date) =>
        ToUtc(date.ToDateTime(TimeOnly.MinValue));

    public static DateTimeOffset? StartOfNextDay(DateOnly date) =>
        date == DateOnly.MaxValue ? null : StartOfDay(date.AddDays(1));

    public static DateOnly DateAt(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);

    public static (DateTimeOffset DayStartUtc, DateTimeOffset WeekStartUtc)
        DayAndWeekBoundariesAt(DateTimeOffset instant)
    {
        var date = DateAt(instant);
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return (StartOfDay(date), StartOfDay(date.AddDays(-daysSinceMonday)));
    }

    public static (DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc) ToUtcRange(
        DateOnly from,
        DateOnly toInclusive)
    {
        if (toInclusive < from)
        {
            throw new ArgumentOutOfRangeException(
                nameof(toInclusive),
                "The inclusive end date cannot precede the start date.");
        }

        var endExclusiveUtc = StartOfNextDay(toInclusive)
            ?? throw new ArgumentOutOfRangeException(
                nameof(toInclusive),
                "The inclusive end date must have a following calendar day.");
        return (StartOfDay(from), endExclusiveUtc);
    }

    public static DateTimeOffset ToUtc(DateTime localTime)
    {
        var local = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        var offset = TimeZone.IsAmbiguousTime(local)
            ? TimeZone.GetAmbiguousTimeOffsets(local).Min()
            : TimeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
