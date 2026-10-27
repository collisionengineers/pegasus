using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// The claim references a retained message names in its subject or body.
/// D14 widens association from the Principal's label-anchored extractors to
/// any token of <c>[A-Za-z0-9/-]</c> carrying a digit, plus the durable QDOS
/// tail that token normalises to, so a message can name a Case's claim
/// reference without the label the Principal's own grammar requires. Free
/// text is bounded because every token becomes one query parameter.
/// </summary>
public static partial class MailClaimReferenceTokens
{
    public const int MaximumTokens = 100;
    public const int MinimumTokenLength = 6;
    public const int MaximumTokenLength = 100;

    public static IReadOnlyList<string> Extract(params string?[] texts)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            foreach (Match match in TokenRegex().Matches(text))
            {
                Add(match.Value);
                if (tokens.Count >= MaximumTokens)
                {
                    return tokens;
                }

                if (PrincipalCaseMatchPolicy.NormalizeClaimReference(match.Value) is { } normalized)
                {
                    Add(normalized);
                    if (tokens.Count >= MaximumTokens)
                    {
                        return tokens;
                    }
                }
            }
        }

        return tokens;

        void Add(string value)
        {
            if (value.Length is < MinimumTokenLength or > MaximumTokenLength
                || !value.Any(char.IsAsciiDigit))
            {
                return;
            }

            if (seen.Add(value))
            {
                tokens.Add(value);
            }
        }
    }

    // A token is a whole run of the allowed characters: a longer run is not
    // truncated into one, and a token has to carry a digit to be a reference.
    [GeneratedRegex(
        @"(?<![A-Za-z0-9/\-])[A-Za-z0-9/\-]{6,100}(?![A-Za-z0-9/\-])",
        RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
