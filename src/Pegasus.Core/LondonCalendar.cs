namespace Pegasus.Core;

/// <summary>
/// Converts Europe/London civil dates and times to deterministic UTC instants. Invalid local
/// times advance to the first valid minute; ambiguous local times select the later instant.
/// </summary>
public static class LondonCalendar
{
    private static readonly TimeZoneInfo TimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public static DateTimeOffset StartOfDay(DateOnly date) =>
        ToUtc(date.ToDateTime(TimeOnly.MinValue));

    public static DateTimeOffset? StartOfNextDay(DateOnly date) =>
        date == DateOnly.MaxValue ? null : StartOfDay(date.AddDays(1));

    public static DateOnly DateAt(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);

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
}
