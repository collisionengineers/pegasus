using System.Text.RegularExpressions;

namespace Pegasus.Core.Vehicle;

/// <summary>
/// The one owner of what counts as a UK registration. Both checks take a value
/// that is already upper case and free of spaces and hyphens; each caller keeps
/// its own normalisation.
/// </summary>
internal static partial class UkRegistrationShapes
{
    /// <summary>
    /// A current (AB12CDE), prefix (A123BCD) or suffix (ABC123D) registration.
    /// </summary>
    internal static bool IsDatedShape(string value) =>
        CurrentRegex().IsMatch(value) || PrefixRegex().IsMatch(value) || SuffixRegex().IsMatch(value);

    /// <summary>
    /// A dated shape, or a dateless one: digits then letters, or letters then
    /// digits.
    /// </summary>
    internal static bool IsAnyShape(string value) =>
        IsDatedShape(value) || DigitsThenLettersRegex().IsMatch(value) || LettersThenDigitsRegex().IsMatch(value);

    [GeneratedRegex(@"^[A-Z]{2}[0-9]{2}[A-Z]{3}$", RegexOptions.CultureInvariant, 100)] private static partial Regex CurrentRegex();
    [GeneratedRegex(@"^[A-Z][0-9]{1,3}[A-Z]{3}$", RegexOptions.CultureInvariant, 100)] private static partial Regex PrefixRegex();
    [GeneratedRegex(@"^[A-Z]{3}[0-9]{1,3}[A-Z]$", RegexOptions.CultureInvariant, 100)] private static partial Regex SuffixRegex();
    [GeneratedRegex(@"^[0-9]{1,4}[A-Z]{1,3}$", RegexOptions.CultureInvariant, 100)] private static partial Regex DigitsThenLettersRegex();
    // This shared syntactic shape covers GB dateless letters-first and Northern
    // Ireland registrations without maintaining a duplicate regular expression.
    [GeneratedRegex(@"^[A-Z]{1,3}[0-9]{1,4}$", RegexOptions.CultureInvariant, 100)] private static partial Regex LettersThenDigitsRegex();
}
