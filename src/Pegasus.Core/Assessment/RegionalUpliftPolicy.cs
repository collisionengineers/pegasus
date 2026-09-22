using System.Text.RegularExpressions;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The regional labour-rate uplift (v28 P17): + 15 % on the hourly rate, an
/// Engineer's choice on the repair specification. The workspace suggests it
/// when the repairer, the claimant or the storage address carries a London
/// or Home Counties postcode; the operator's list of outward codes is the
/// reference file's.
/// </summary>
public static partial class RegionalUpliftPolicy
{
    private static readonly HashSet<string> WholeAreas = new(StringComparer.Ordinal)
    {
        "SS", "RM", "DA", "BR", "CR", "SM", "KT", "TW", "SL", "UB", "HA", "WD", "AL", "EN", "IG", "LU", "HP",
        "GU", "RH", "BN", "TN", "ME", "CT", "E", "EC", "N", "NW", "SE", "SW", "W", "WC"
    };

    private static readonly Dictionary<string, int[]> Districts = new(StringComparer.Ordinal)
    {
        ["SG"] = [1, 2, 3, 4, 5, 9, 10, 11, 12, 13, 14],
        ["OX"] = [1, 3, 4, 5, 9, 10, 11, 14, 25, 39, 44, 49],
        ["RG"] = [1, 2, 4, 5, 6, 7, 8, 9, 10, 12, 18, 21, 22, 23, 24, 25, 27, 29, 30, 31, 40, 41, 42, 45],
        ["CM"] = [0, 9]
    };

    /// <summary>The outward code found in a free-text address, or null when the text carries no UK postcode.</summary>
    public static string? OutwardCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var match = Postcode().Match(text.ToUpperInvariant());
        return match.Success ? match.Groups["area"].Value + match.Groups["district"].Value : null;
    }

    /// <summary>Whether an address's postcode lies in London or the Home Counties.</summary>
    public static bool Suggests(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var match = Postcode().Match(text.ToUpperInvariant());
        if (!match.Success)
        {
            return false;
        }
        var area = match.Groups["area"].Value;
        return WholeAreas.Contains(area)
            || (Districts.TryGetValue(area, out var districts)
                && int.TryParse(match.Groups["district"].Value, out var district)
                && districts.Contains(district));
    }

    [GeneratedRegex(@"\b(?<area>[A-Z]{1,2})(?<district>\d{1,2})[A-Z]?\s*\d[A-Z]{2}\b")]
    private static partial Regex Postcode();
}
