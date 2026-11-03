using Azure.Core;
using Azure.Identity;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// The app-only Graph credential of the hosted DevelopmentOffline test instance
/// (ADR-0088). Production authenticates with its managed identities, which exist
/// only inside Azure; a run on the operator's own machine presents an Entra app
/// registration's client secret instead. That registration holds no tenant-wide
/// mail permission: Exchange RBAC grants it <c>Application Mail.Read</c>,
/// <c>Mail.ReadWrite</c> and <c>Mail.Send</c> on the approved mailboxes alone, so
/// the instance has Graph parity with production on those mailboxes and reaches
/// no other (R648, R650). The keys are parsed at host build, as every other live
/// opt-in is, so a missing one fails the start naming the key rather than the
/// first poll.
/// </summary>
public static class LocalGraphCredential
{
    public const string FeatureKey = "Features:LiveGraphMail";
    public const string TenantIdKey = "Graph:TenantId";
    public const string ClientIdKey = "Graph:ClientId";
    public const string ClientSecretKey = "Graph:ClientSecret";
    public const string ChangeNotificationUrlKey = "Graph:ChangeNotificationUrl";
    public const string ChangeNotificationClientStateKey = "Graph:ChangeNotificationClientState";

    /// <summary>The webhook's own bound on the client state it compares.</summary>
    private const int MaximumClientStateLength = 128;

    public static TokenCredential Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var tenantId = RequireGuid(read, TenantIdKey);
        var clientId = RequireGuid(read, ClientIdKey);
        var clientSecret = read(ClientSecretKey);
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException($"{ClientSecretKey} is required for {FeatureKey}.");
        }

        return new ClientSecretCredential(tenantId.ToString("D"), clientId.ToString("D"), clientSecret);
    }

    /// <summary>
    /// Whether the instance maintains Graph change subscriptions: both webhook
    /// keys present and well formed. With neither, the run is poll-only and the
    /// maintenance pass has no provider to call; with one, the start fails naming
    /// the other, rather than every five-minute pass failing before the inbox
    /// poll. The URL is the Funnelled webhook path on the instance's public host
    /// (R649), which Microsoft Graph must reach from the internet.
    /// </summary>
    public static bool HasChangeNotifications(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var url = read(ChangeNotificationUrlKey);
        var clientState = read(ChangeNotificationClientStateKey);
        var hasUrl = !string.IsNullOrWhiteSpace(url);
        var hasClientState = !string.IsNullOrWhiteSpace(clientState);
        if (!hasUrl && !hasClientState)
        {
            return false;
        }
        if (!hasUrl)
        {
            throw new InvalidOperationException(
                $"{ChangeNotificationUrlKey} is required with {ChangeNotificationClientStateKey} for {FeatureKey}.");
        }
        if (!hasClientState)
        {
            throw new InvalidOperationException(
                $"{ChangeNotificationClientStateKey} is required with {ChangeNotificationUrlKey} for {FeatureKey}.");
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || uri.IsLoopback)
        {
            throw new InvalidOperationException(
                $"{ChangeNotificationUrlKey} must be an absolute https URL that Microsoft Graph can reach for {FeatureKey}.");
        }
        if (clientState!.Length > MaximumClientStateLength)
        {
            throw new InvalidOperationException(
                $"{ChangeNotificationClientStateKey} must be at most {MaximumClientStateLength} characters for {FeatureKey}.");
        }

        return true;
    }

    private static Guid RequireGuid(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is required for {FeatureKey}.");
        }
        if (!Guid.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"{key} must be a GUID for {FeatureKey}.");
        }

        return parsed;
    }
}
