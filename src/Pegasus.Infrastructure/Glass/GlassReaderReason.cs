using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The export reader's reason for refusing a document, cut down to what the host
/// log may hold. The reader's messages name a position number, a field and the
/// code value it met; a value is the provider's own text, so it is kept only
/// when it is a short code and is never the Case's registration. A position
/// type or repair kind is Glass's own name and may hold spaces (<c>Old for
/// New</c>), so after those words a short name of letters, digits, spaces and
/// underscores is kept too, and the log says which one the reader met (R1209).
/// </summary>
internal static partial class GlassReaderReason
{
    /// <summary>The longest reason the log keeps.</summary>
    public const int MaximumLength = 240;

    private const int MaximumValueLength = 40;

    [GeneratedRegex("(position type |repair kind )?'([^']*)'")]
    private static partial Regex QuotedValue();

    /// <summary>
    /// The reason with every quoted value that is not a short code (or, after
    /// "position type" or "repair kind", a short name) replaced by
    /// <c>?</c>, the registration removed wherever it appears, anything outside
    /// printable ASCII dropped and the whole cut to <see cref="MaximumLength"/>.
    /// </summary>
    public static string Of(string reason, string registration)
    {
        ArgumentNullException.ThrowIfNull(reason);
        var compactRegistration = string.Concat((registration ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));
        var kept = QuotedValue().Replace(reason, match =>
            IsCode(match.Groups[2].Value)
            || (match.Groups[1].Success && IsName(match.Groups[2].Value, compactRegistration))
                ? match.Value
                : match.Groups[1].Value + "'?'");
        if (compactRegistration.Length > 0)
        {
            kept = kept.Replace(compactRegistration, "?", StringComparison.OrdinalIgnoreCase);
            if (registration is { Length: > 0 } && registration != compactRegistration)
            {
                kept = kept.Replace(registration, "?", StringComparison.OrdinalIgnoreCase);
            }
        }

        var printable = new StringBuilder(kept.Length);
        foreach (var character in kept)
        {
            if (character is >= ' ' and <= '~')
            {
                printable.Append(character);
            }
        }

        return printable.Length <= MaximumLength ? printable.ToString() : printable.ToString(0, MaximumLength);
    }

    /// <summary>A short name of letters, digits, spaces and underscores that does not spell the registration with its spaces moved.</summary>
    private static bool IsName(string value, string compactRegistration) =>
        value.Length is > 0 and <= MaximumValueLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '_')
        && (compactRegistration.Length == 0
            || !value.Replace(" ", string.Empty, StringComparison.Ordinal)
                .Contains(compactRegistration, StringComparison.OrdinalIgnoreCase));

    private static bool IsCode(string value) =>
        value.Length is > 0 and <= MaximumValueLength
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.' or ':');
}
