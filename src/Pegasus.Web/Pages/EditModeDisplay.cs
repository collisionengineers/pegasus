using System.Globalization;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages;

/// <summary>
/// Operator wording for the one server-owned edit authority a case carries. Other authorised staff
/// stay read-only and are told who is editing and when editing becomes available; the holder is
/// named by staff account only, never by identifier, and an unresolved holder is described without
/// one. There is no takeover control anywhere this copy appears.
/// </summary>
public static class EditModeDisplay
{
    private static readonly TimeZoneInfo LondonTimeZone = ResolveLondonTimeZone();

    /// <summary>Europe/London wall-clock time, as the rest of the application renders instants.</summary>
    public static string WallClock(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, LondonTimeZone)
            .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string HeldBy(
        CaseEditAuthorityHolder holder,
        DateTimeOffset availableAtUtc,
        bool isSelf)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return isSelf
            ? $"You are editing this case. Editing stays yours until {WallClock(availableAtUtc)}."
            : $"{Editor(holder, capitalized: true)} is editing this case. "
                + $"Editing becomes available at {WallClock(availableAtUtc)}.";
    }

    /// <summary>
    /// The same disclosure where a case is reached from another workspace and the sentence has to
    /// say that case editing, not this page, is what is unavailable.
    /// </summary>
    public static string CaseHeldBy(
        CaseEditAuthorityHolder holder,
        DateTimeOffset availableAtUtc,
        bool isSelf)
    {
        ArgumentNullException.ThrowIfNull(holder);
        return isSelf
            ? "Case editing is unavailable here because you are editing the case elsewhere. "
                + $"Editing stays yours until {WallClock(availableAtUtc)}."
            : $"Case editing is unavailable while {Editor(holder, capitalized: false)} is editing "
                + $"this case. Editing becomes available at {WallClock(availableAtUtc)}.";
    }

    private static string Editor(CaseEditAuthorityHolder holder, bool capitalized) =>
        string.IsNullOrWhiteSpace(holder.DisplayName)
            ? capitalized ? "Another member of staff" : "another member of staff"
            : holder.DisplayName;

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
