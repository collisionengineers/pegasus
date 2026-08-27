using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Eva;

/// <summary>
/// What the EVA API adapter needs to exist (EXT-04).
///
/// Shaped like <see cref="Vehicle.DvlaDvsaProductionOptions"/> on purpose: a
/// positional record with one validating factory, so a malformed value fails
/// at composition with the offending key named rather than at the first
/// submission with a null reference.
///
/// EVA publishes **one** host for both its test and live environments — the
/// environment is decided entirely by which credential pair is presented. That
/// is why the host is pinned here and the credentials are not: pointing this
/// adapter somewhere else is never legitimate, whereas swapping the
/// credentials is exactly how going live works.
/// </summary>
public sealed record EvaApiOptions(
    Uri BaseUri,
    string ClientId,
    string ClientSecret,
    EvaInstructionSettings Instruction)
{
    /// <summary>The only host EVA's API is served from.</summary>
    public const string ApprovedHost = "sentry.evasoftware.co.uk";

    /// <summary>
    /// EVA's accepted inspection types. A value outside this set is refused by
    /// EVA with a 400, so it is refused here first, where the error names the
    /// configuration key instead of arriving as a rejected case.
    /// </summary>
    private static readonly string[] InspectionTypes =
    [
        "Vehicle Damage Inspection",
        "Inspect and Authorise",
        "Inspect Only",
        "WOP Inspection",
        "Rectification work",
        "Quality/Audit Inspection",
        "Post Repair",
        "Low Velocity Inspection",
        "Desktop",
        "Other"
    ];

    public Uri TokenUri => new(BaseUri, "Connect/token");

    public Uri InstructionUri => new(BaseUri, "Instruction/Inspection");

    public static EvaApiOptions Create(IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new(
            RequireApprovedBaseUri(values, "Eva:BaseUri"),
            Require(values, "Eva:ClientId"),
            Require(values, "Eva:ClientSecret"),
            new(
                Require(values, "Eva:RequestFrom"),
                RequireInspectionType(values, "Eva:InspectionType"),
                RequireEmail(values, "Eva:InstructionEmail")));
    }

    private static string Require(IReadOnlyDictionary<string, string?> values, string key)
    {
        if (!values.TryGetValue(key, out var value)
            || string.IsNullOrWhiteSpace(value)
            || value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} is required for the EVA API adapter.");
        }

        return value.Trim();
    }

    /// <summary>
    /// The base URI, pinned to EVA's host and normalised to a trailing slash.
    ///
    /// The slash is not cosmetic: <see cref="Uri(Uri, string)"/> resolves a
    /// relative path against the *directory* of the base, so a base of
    /// ".../api" without the slash would resolve "Connect/token" to
    /// ".../Connect/token" and silently drop the "/api" segment.
    /// </summary>
    private static Uri RequireApprovedBaseUri(
        IReadOnlyDictionary<string, string?> values,
        string key)
    {
        var value = Require(values, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTPS URI.");
        }
        if (!uri.Host.Equals(ApprovedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{key} must use the approved EVA host.");
        }

        return uri.AbsolutePath.EndsWith('/')
            ? uri
            : new UriBuilder(uri) { Path = uri.AbsolutePath + "/" }.Uri;
    }

    private static string RequireInspectionType(
        IReadOnlyDictionary<string, string?> values,
        string key)
    {
        var value = Require(values, key);
        return InspectionTypes.Contains(value, StringComparer.Ordinal)
            ? value
            : throw new InvalidOperationException(
                $"{key} must be one of EVA's accepted inspection types.");
    }

    /// <summary>
    /// Where EVA sends the instruction. Deliberately not a case value: it is
    /// the operator's own address, and reading it off a case would mail the
    /// instruction to whoever last appeared on one.
    /// </summary>
    private static string RequireEmail(
        IReadOnlyDictionary<string, string?> values,
        string key)
    {
        var value = Require(values, key);
        var separator = value.IndexOf('@', StringComparison.Ordinal);
        return separator > 0
            && separator < value.Length - 1
            && !value.Contains(' ', StringComparison.Ordinal)
                ? value
                : throw new InvalidOperationException($"{key} must be an email address.");
    }
}
