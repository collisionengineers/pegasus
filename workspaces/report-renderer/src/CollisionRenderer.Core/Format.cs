using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CollisionRenderer.Core.Models;

namespace CollisionRenderer.Core;

/// <summary>
/// Presentation helpers — currency, mileage, year, vehicle labels, history
/// normalisation. Ported from the proven valuation renderer so output reads
/// identically. Every text helper HTML-encodes by default; callers that build
/// markup use <see cref="Raw"/> deliberately.
/// </summary>
public static class Format
{
    private static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>
    /// The UK business time zone. Document dates are the UK business date wherever
    /// the render runs, so a UTC container and a UK desktop during BST agree on the
    /// date near midnight instead of disagreeing by a day.
    /// </summary>
    private static readonly TimeZoneInfo UkZone = ResolveUkZone();

    private static TimeZoneInfo ResolveUkZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (TimeZoneNotFoundException)
        {
            // A Windows host without the IANA mapping data still carries the
            // Windows identifier for the same zone.
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }

    public static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string Attr(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Returns an attribute-encoded URL only when its scheme is safe (http/https/
    /// mailto or scheme-relative); otherwise empty. Blocks <c>javascript:</c>,
    /// <c>data:</c> and <c>file:</c> hrefs from reaching the rendered document.
    /// </summary>
    public static string SafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var v = value.Trim();
        var lower = v.ToLowerInvariant();
        var safe = lower.StartsWith("http://") || lower.StartsWith("https://")
                   || lower.StartsWith("mailto:") || !lower.Contains(':');
        return safe ? Attr(v) : string.Empty;
    }

    public static string Money(string? value, bool decimals = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, "(?i)\\bgbp\\s*", string.Empty)
            .Replace("£", string.Empty)
            .Replace(",", string.Empty)
            .Trim();

        if (decimal.TryParse(cleaned, NumberStyles.Any, Uk, out var amount))
        {
            return amount.ToString(decimals ? "C2" : "C0", Uk);
        }

        return Enc(value);
    }

    public static string Money(decimal amount, bool decimals = true) =>
        amount.ToString(decimals ? "C2" : "C0", Uk);

    public static string? OptionalMoney(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Money(value);

    public static string Mileage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 0 && long.TryParse(digits, out var miles) && digits.Length == value.Trim().Length)
        {
            return miles.ToString("N0", Uk);
        }

        return Enc(value);
    }

    public static string SubjectMileage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Not stated";
        }

        var display = Mileage(value);
        if (!display.Any(char.IsDigit))
        {
            return display;
        }

        return display.Contains("mile", StringComparison.OrdinalIgnoreCase) ? display : $"{display} miles";
    }

    public static string Year(string? value)
    {
        var text = value ?? string.Empty;
        var match = Regex.Match(text, "\\b(19|20)\\d{2}\\b");
        return match.Success ? match.Value : Enc(text);
    }

    /// <summary>
    /// The current UK business date, taken from <paramref name="timeProvider"/> in UTC
    /// and converted to Europe/London. Inject a fixed provider to make a document date
    /// testable.
    /// </summary>
    public static string Today(TimeProvider timeProvider) =>
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), UkZone).ToString("dd/MM/yyyy", Uk);

    /// <summary>
    /// The current UK business date from the ambient system clock. This is the
    /// documented fallback only; prefer an explicit caller-supplied document date,
    /// then the <see cref="TimeProvider"/> overload.
    /// </summary>
    public static string Today() => Today(TimeProvider.System);

    public static string VehicleLabel(Advert advert) =>
        string.Join(" ", new[] { advert.Make, advert.Model, advert.DerivativeOrEngine }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));

    public static string SubjectDisplayName(SubjectVehicle subject)
    {
        if (!string.IsNullOrWhiteSpace(subject.VehicleDescription))
        {
            return subject.VehicleDescription!.Trim();
        }

        return string.Join(" ", new[] { subject.Make, subject.Model, subject.Derivative }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private const string DefaultHistory = "Assumed full service history unless stated otherwise";

    private static readonly string[] CleanHistory =
    {
        "no adverse history", "no adverse recorded", "clear vehicle history", "clear history",
        "history check clear", "history clear",
    };

    private static readonly string[] MaterialHistory =
    {
        "category", "cat s", "cat n", "write-off", "write off", "written off", "insurance loss",
        "stolen", "scrapped", "salvage", "mileage discrepancy", "discrepancy", "clock", "import",
        "export", "damage", "outstanding finance",
    };

    public static string VehicleHistory(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return DefaultHistory;
        }

        var normalised = string.Join(" ", text.ToLowerInvariant().Replace("-", " ").Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var clean = CleanHistory.Any(m => normalised.Contains(m));
        var material = MaterialHistory.Any(m => normalised.Contains(m));
        if (clean && !material)
        {
            return "No adverse history recorded";
        }

        return text;
    }

    /// <summary>Caller asserts the string is already safe HTML.</summary>
    public static string Raw(string? html) => html ?? string.Empty;
}
