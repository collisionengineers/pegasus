using System.Globalization;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Assessment;

/// <summary>
/// One band of a Principal's salvage matrix: the share of the pre-accident
/// value (the Engineer's Value) paid to the customer and deducted from the
/// settlement, for a value from <paramref name="From"/> to
/// <paramref name="To"/> inclusive in one salvage category.
/// </summary>
public sealed record SalvageMatrixBand(
    string Category,
    decimal From,
    decimal To,
    decimal Percentage);

/// <summary>One row as an administrator typed it; blank rows are ignored.</summary>
public sealed record SalvageMatrixEntry(
    string Category,
    string? From,
    string? To,
    string? Percentage)
{
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(From) && string.IsNullOrWhiteSpace(To) && string.IsNullOrWhiteSpace(Percentage);
}

/// <summary>The rule a refused salvage matrix broke.</summary>
public enum SalvageMatrixRule
{
    UnknownCategory,
    Incomplete,
    InvalidAmount,
    FromAfterTo,
    InvalidPercentage,
    Overlap
}

public sealed class SalvageMatrixException(
    SalvageMatrixRule rule,
    string category,
    SalvageMatrixBand? band = null,
    SalvageMatrixBand? otherBand = null)
    : ArgumentException("The salvage matrix is not valid.")
{
    public SalvageMatrixRule Rule { get; } = rule;
    public string Category { get; } = category;
    public SalvageMatrixBand? Band { get; } = band;
    public SalvageMatrixBand? OtherBand { get; } = otherBand;
}

/// <summary>
/// A Principal's salvage matrix (operator, 29 September 2026): for each
/// salvage category, the Engineer's Value bands and the percentage of that
/// value the salvage is worth. A Principal with one has the Case's salvage
/// value filled from it; a Principal without one has none, and the salvage
/// value is typed as before. The only cross-row rule is that a category's
/// bands do not overlap; a gap simply fills nothing.
/// </summary>
public sealed record SalvageMatrix(IReadOnlyList<SalvageMatrixBand> Bands)
{
    /// <summary>The salvage categories a matrix bands: the Case's own codes, less N/A.</summary>
    public static IReadOnlyList<string> Categories { get; } =
        [.. AssessmentVocabulary.Definitions[AssessmentVocabulary.SalvageCategory].Codes!
            .Where(code => code != AssessmentReportContract.NoSalvageCategory)];

    public bool Equals(SalvageMatrix? other) =>
        other is not null && Bands.SequenceEqual(other.Bands);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var band in Bands)
        {
            hash.Add(band);
        }
        return hash.ToHashCode();
    }

    /// <summary>
    /// The matrix the typed rows describe, or null when every row is blank.
    /// A partly filled row, or a number that does not read, is refused under
    /// the rule its column carries.
    /// </summary>
    public static SalvageMatrix? FromEntries(IEnumerable<SalvageMatrixEntry> entries)
    {
        var bands = new List<SalvageMatrixBand>();
        foreach (var entry in entries.Where(entry => !entry.IsBlank))
        {
            if (string.IsNullOrWhiteSpace(entry.From)
                || string.IsNullOrWhiteSpace(entry.To)
                || string.IsNullOrWhiteSpace(entry.Percentage))
            {
                throw new SalvageMatrixException(SalvageMatrixRule.Incomplete, entry.Category);
            }
            bands.Add(new(
                entry.Category,
                ReadNumber(entry.From, SalvageMatrixRule.InvalidAmount, entry.Category),
                ReadNumber(entry.To, SalvageMatrixRule.InvalidAmount, entry.Category),
                ReadNumber(entry.Percentage, SalvageMatrixRule.InvalidPercentage, entry.Category)));
        }

        return Normalize(bands);
    }

    /// <summary>
    /// The one shape a stored matrix takes: every band in a known category,
    /// amounts of £0.00 or more in pounds and pence, From not after To, a
    /// percentage from 0 to 100 to two places, no two bands of a category
    /// overlapping (each band includes both ends), sorted by category and
    /// then From. No bands at all is no matrix.
    /// </summary>
    public static SalvageMatrix? Normalize(IEnumerable<SalvageMatrixBand>? bands)
    {
        var list = (bands ?? []).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        foreach (var band in list)
        {
            if (!Categories.Contains(band.Category, StringComparer.Ordinal))
            {
                throw new SalvageMatrixException(SalvageMatrixRule.UnknownCategory, band.Category ?? string.Empty);
            }
            if (!IsPounds(band.From) || !IsPounds(band.To))
            {
                throw new SalvageMatrixException(SalvageMatrixRule.InvalidAmount, band.Category);
            }
            if (band.From > band.To)
            {
                throw new SalvageMatrixException(SalvageMatrixRule.FromAfterTo, band.Category);
            }
            if (band.Percentage is < 0m or > 100m || decimal.Round(band.Percentage, 2) != band.Percentage)
            {
                throw new SalvageMatrixException(SalvageMatrixRule.InvalidPercentage, band.Category);
            }
        }

        var sorted = Categories
            .SelectMany(category => list
                .Where(band => band.Category == category)
                .OrderBy(band => band.From)
                .ThenBy(band => band.To))
            .ToArray();
        // Sorted by From, a band overlaps when it starts at or before the
        // previous band of its category ends.
        SalvageMatrixBand? previous = null;
        foreach (var band in sorted)
        {
            if (previous is not null && previous.Category == band.Category && band.From <= previous.To)
            {
                throw new SalvageMatrixException(SalvageMatrixRule.Overlap, band.Category, previous, band);
            }
            previous = band;
        }

        return new(sorted);
    }

    /// <summary>
    /// The salvage value the matrix gives on a total loss: the Engineer's
    /// Value times the percentage of the category's band holding it, to the
    /// penny, a half penny rounding away from zero. Null for any other
    /// outcome, or when no band holds the value.
    /// </summary>
    public decimal? SalvageValueFor(string? outcome, string? category, decimal? engineerValue) =>
        outcome == TotalLoss
        && engineerValue is { } value
        && Bands.FirstOrDefault(band => band.Category == category && band.From <= value && value <= band.To) is { } band
            ? Math.Round(value * band.Percentage / 100m, 2, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>
    /// Whether a salvage value is still the matrix's to fill: an empty box, or
    /// the figure the matrix gives now. Any other figure is someone's own and
    /// the matrix leaves it alone.
    /// </summary>
    public bool Follows(string? outcome, string? category, decimal? engineerValue, decimal? salvageValue) =>
        salvageValue is null || SalvageValueFor(outcome, category, engineerValue) == salvageValue;

    private const string TotalLoss = "total_loss";

    private static bool IsPounds(decimal amount) =>
        amount >= 0m && decimal.Round(amount, 2) == amount;

    private static decimal ReadNumber(string text, SalvageMatrixRule rule, string category) =>
        decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new SalvageMatrixException(rule, category);
}

/// <summary>The salvage matrix of the Principal a Case belongs to.</summary>
public interface IPrincipalSalvageMatrixQueries
{
    Task<SalvageMatrix?> GetForCaseAsync(Guid caseId, CancellationToken cancellationToken);
}
