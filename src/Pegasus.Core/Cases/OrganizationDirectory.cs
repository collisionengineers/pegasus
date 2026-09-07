using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

public enum OrganizationDirectoryRole { Repairer, Storage, InspectionLocation }
public sealed record OrganizationDirectoryRecord(
    Guid Id, OrganizationDirectoryRole Role, string Name, string? ContactName,
    string? Telephone, string? Email, string Address, string? Postcode,
    bool Active, long Version, string SourceKind, Guid SourceRecordId,
    long SourceVersion, DateTimeOffset UpdatedAtUtc);
public sealed record OrganizationDirectoryQuery(
    ActionActor Actor, string Prefix, OrganizationDirectoryRole? Role, int Limit = 20);
public interface IOrganizationDirectoryQueries
{
    Task<IReadOnlyList<OrganizationDirectoryRecord>> SearchAsync(
        OrganizationDirectoryQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// EXT-18/S05: the shared local-suggestion matching rule. No fuzzy or
/// geographic inference — a trimmed, collapsed-whitespace, case-insensitive
/// name prefix, or an uppercase, whitespace-free postcode prefix — and the
/// same internal 20-row cap every local address-suggestion source obeys,
/// never a caller-configurable one.
/// </summary>
public static class InspectionLocationMatchPolicy
{
    public const int MinimumNormalizedPrefixLength = 2;
    public const int MaximumResultLimit = 20;

    public static string NormalizeNamePrefix(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return CollapseWhitespace(value.Trim()).ToUpperInvariant();
    }

    public static string NormalizePostcodePrefix(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray())
            .ToUpperInvariant();
    }

    public static bool MeetsMinimumLength(string normalizedPrefix) =>
        normalizedPrefix.Length >= MinimumNormalizedPrefixLength;

    /// <summary>
    /// The one exact-before-prefix rule every local suggestion source ranks
    /// by: a whole normalized name equal to the search prefix, or a whole
    /// normalized postcode equal to it. Shared by
    /// <c>EfOrganizationDirectory</c> and <c>InspectionAddressChoicesQueries</c>
    /// so the rule has one owner (C06 review R-10) — an EF Core query that
    /// needs this in a SQL <c>ORDER BY</c> cannot call it directly (it is not
    /// translatable), so that one call site repeats the same expression
    /// inline and is the only place allowed to.
    /// </summary>
    public static bool IsExactMatch(
        string normalizedName,
        string? normalizedPostcode,
        string namePrefix,
        string postcodePrefix) =>
        normalizedName == namePrefix
        || (normalizedPostcode is not null && normalizedPostcode == postcodePrefix);

    public static int ClampLimit(int requestedLimit) =>
        Math.Clamp(requestedLimit, 1, MaximumResultLimit);

    private static string CollapseWhitespace(string value)
    {
        Span<char> buffer = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];
        var written = 0;
        var previousWasWhitespace = false;
        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && written > 0)
                {
                    buffer[written++] = ' ';
                }
                previousWasWhitespace = true;
                continue;
            }

            buffer[written++] = character;
            previousWasWhitespace = false;
        }

        if (written > 0 && buffer[written - 1] == ' ')
        {
            written--;
        }

        return new string(buffer[..written]);
    }
}
