namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// What the Glass's repair-estimate adapter needs to exist (CASE-047 B04).
///
/// Shaped like <see cref="Eva.EvaApiOptions"/> on purpose: a positional record
/// with one validating factory, so a malformed value fails at composition with
/// the offending key named rather than at the first launch with a null
/// reference.
///
/// <para>
/// <b>Nothing here is hard-coded.</b> Both provider origins and Pegasus's own
/// callback origin arrive as configuration, because they are the three hosts
/// every origin check in this adapter is made against: a stage's redirect, the
/// estimator launch URL and the rewritten <c>caller</c> are each compared to
/// one of them. Pinning a host in code would put the same list in two places
/// and make the check untestable without reaching the live provider.
/// </para>
/// </summary>
public sealed record GlassRepairEstimateOptions(
    Uri MarketValueAssessorBaseUri,
    Uri EstimatorBaseUri,
    Uri CallbackBaseUri,
    string RepairProfileId,
    TimeSpan SessionLifetime,
    TimeSpan ExportPollInterval,
    TimeSpan ExportTimeout,
    int MaximumExportBytes)
{
    /// <summary>
    /// The named client the gateway resolves. Its handler must not follow
    /// redirects and must not manage cookies: a Glass's session's cookie jar is
    /// per session and durable, so it is carried in this adapter's protected
    /// state rather than in a pooled handler shared by every session.
    /// </summary>
    public const string HttpClientName = "glass.mva";

    /// <summary>
    /// The Pegasus page the provider's <c>caller</c> is rewritten to. The
    /// one-use correlation token is the last path segment, matching the
    /// callback route B publishes at <c>/Integrations/Glass/Callback/{correlation}</c>.
    /// </summary>
    public const string CallbackPath = "Integrations/Glass/Callback/";

    /// <summary>
    /// A host hands over its own configuration lookup rather than restating the
    /// key names, so which keys Glass's needs is decided here and every host
    /// asks for exactly those.
    /// </summary>
    public static GlassRepairEstimateOptions Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(
            RequireHttpsOrigin(read, "Glass:MarketValueAssessorBaseUri"),
            RequireHttpsOrigin(read, "Glass:EstimatorBaseUri"),
            RequireHttpsOrigin(read, "Glass:CallbackBaseUri"),
            RequireProfile(read, "Glass:RepairProfileId"),
            TimeSpan.FromHours(RequirePositive(read, "Glass:SessionHours", 8)),
            TimeSpan.FromSeconds(RequirePositive(read, "Glass:ExportPollSeconds", 2)),
            TimeSpan.FromSeconds(RequirePositive(read, "Glass:ExportTimeoutSeconds", 60)),
            // The export is read by GlassEstimateXmlParser, which refuses a
            // document larger than this; a second, different cap here would be
            // a second copy of the same rule.
            RequirePositive(read, "Glass:MaximumExportBytes", GlassEstimateXmlParser.MaximumDocumentBytes));
    }

    /// <summary>The callback this launch will accept, carrying its one-use token.</summary>
    public Uri CallbackFor(string correlationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationToken);
        return new Uri(CallbackBaseUri, CallbackPath + correlationToken);
    }

    public bool IsMarketValueAssessor(Uri uri) => IsOrigin(uri, MarketValueAssessorBaseUri);

    public bool IsEstimator(Uri uri) => IsOrigin(uri, EstimatorBaseUri);

    /// <summary>An absolute request address on the Market Value Assessor.</summary>
    public Uri MarketValueAssessor(string relativePath) =>
        new(MarketValueAssessorBaseUri, relativePath);

    private static bool IsOrigin(Uri uri, Uri origin)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return uri.IsAbsoluteUri
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals(origin.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == origin.Port;
    }

    /// <summary>
    /// An absolute HTTPS origin normalised to a trailing slash. The slash is
    /// not cosmetic: <see cref="Uri(Uri, string)"/> resolves a relative path
    /// against the <i>directory</i> of the base, so a base without it silently
    /// drops its last segment.
    /// </summary>
    private static Uri RequireHttpsOrigin(Func<string, string?> read, string key)
    {
        var value = Require(read, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"{key} must be an absolute HTTPS URI.");
        }

        return uri.AbsolutePath.EndsWith('/')
            ? uri
            : new UriBuilder(uri) { Path = uri.AbsolutePath + "/" }.Uri;
    }

    /// <summary>
    /// The MVA repair profile a new estimate is started against. It is account
    /// configuration, not a vehicle or estimate identity, and a missing or
    /// changed profile stops the launch rather than silently choosing another.
    /// </summary>
    private static string RequireProfile(Func<string, string?> read, string key)
    {
        var value = Require(read, key);
        return value.All(char.IsAsciiDigit)
            ? value
            : throw new InvalidOperationException($"{key} must be the numeric MVA repair profile id.");
    }

    private static int RequirePositive(Func<string, string?> read, string key, int fallback)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : throw new InvalidOperationException($"{key} must be a positive whole number.");
    }

    private static string Require(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} is required for the Glass's estimate adapter.");
        }

        return value.Trim();
    }
}
