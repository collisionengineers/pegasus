using System.Globalization;

namespace Pegasus.Web.Pages;

/// <summary>
/// Operator wording for the one server-owned edit authority a case carries. Other authorised staff
/// stay read-only and are told who is editing and when editing becomes available; nothing beyond
/// the holder identity staff already see is disclosed, and there is no takeover control.
/// </summary>
public static class EditModeDisplay
{
    private static readonly TimeZoneInfo LondonTimeZone = ResolveLondonTimeZone();

    /// <summary>Europe/London wall-clock time, as the rest of the application renders instants.</summary>
    public static string WallClock(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, LondonTimeZone).ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string HeldBy(string holder, DateTimeOffset availableAtUtc, bool isSelf) =>
        isSelf
            ? $"You are editing this case. Editing stays yours until {WallClock(availableAtUtc)}."
            : $"{holder} is editing this case. Editing becomes available at {WallClock(availableAtUtc)}.";

    private static TimeZoneInfo ResolveLondonTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
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
