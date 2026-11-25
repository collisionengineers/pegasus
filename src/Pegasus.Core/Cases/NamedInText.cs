namespace Pegasus.Core.Cases;

/// <summary>
/// The one rule for whether a text names a name: capitals and spaces are
/// ignored, so "Car Claims" names the contact "CarClaims". The fill of a new
/// Case's Claim Source and Repairer from the one linked contact its
/// instruction's text names reads it (operator, 25 November 2026; R1264);
/// report sending rules no longer read the instruction's text (R1268).
/// </summary>
public static class NamedInText
{
    /// <summary>
    /// The policy identity a contact filled from the instruction's text
    /// records on the Case (R1264).
    /// </summary>
    public const string PolicyKey = "intake-contact-named-in-text";

    /// <summary>The version of <see cref="PolicyKey"/>.</summary>
    public const int PolicyVersion = 1;

    /// <summary>
    /// <paramref name="value"/> without its whitespace and in capitals: the
    /// form a text and a name are compared in.
    /// </summary>
    public static string Compact(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
    }

    /// <summary>
    /// Whether <paramref name="compactText"/>, already <see cref="Compact"/>,
    /// names <paramref name="name"/>. A name with nothing but whitespace names
    /// nothing.
    /// </summary>
    public static bool Names(string compactText, string name)
    {
        ArgumentNullException.ThrowIfNull(compactText);
        var needle = Compact(name);
        return needle.Length > 0 && compactText.Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>
    /// The one contact of <paramref name="activeContacts"/> linked to the
    /// Principal with <paramref name="principalCode"/> that
    /// <paramref name="text"/> names, or null when it names none of them or
    /// two or more (R1264).
    /// </summary>
    public static ContactChoice? LinkedContact(
        string? text,
        string? principalCode,
        IReadOnlyList<ContactChoice> activeContacts)
    {
        ArgumentNullException.ThrowIfNull(activeContacts);
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(principalCode))
        {
            return null;
        }

        var compactText = Compact(text);
        var named = activeContacts
            .Where(contact => contact.IsLinkedTo(principalCode) && Names(compactText, contact.Name))
            .Take(2)
            .ToArray();
        return named.Length == 1 ? named[0] : null;
    }
}
