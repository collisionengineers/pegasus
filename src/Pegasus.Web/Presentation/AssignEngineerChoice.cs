namespace Pegasus.Web.Presentation;

/// <summary>
/// The value an Assign Engineer list posts (#1213): an Engineer's staff
/// identifier, or the Unassigned option's value, which is not one. The Work
/// Centre's and the Case record's handlers both read it through
/// <see cref="Parse"/>, so the two lists cannot mean different things.
/// </summary>
public static class AssignEngineerChoice
{
    /// <summary>The Unassigned option's value.</summary>
    public const string UnassignedValue = "unassigned";

    /// <summary>
    /// The Engineer the value names, or null for Unassigned. Anything else,
    /// the list's empty first option included, is refused.
    /// </summary>
    public static Guid? Parse(string? value)
    {
        var trimmed = value?.Trim();
        if (string.Equals(trimmed, UnassignedValue, StringComparison.Ordinal))
        {
            return null;
        }

        return Guid.TryParse(trimmed, out var engineerId) && engineerId != Guid.Empty
            ? engineerId
            : throw new ArgumentException("An Engineer or Unassigned is required.", nameof(value));
    }
}
