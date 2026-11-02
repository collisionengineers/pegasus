using Azure.Core;
using Azure.Identity;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// The app-only Graph credential of a hosted DevelopmentOffline test instance
/// (ADR-0088). Production authenticates with its managed identity, which exists
/// only inside Azure; a run on the operator's own machine presents an Entra app
/// registration's client secret instead. That registration holds no tenant-wide
/// mail permission: Exchange RBAC grants it <c>Application Mail.Read</c> on the
/// approved mailboxes alone, so even a wrongly composed host could not send or
/// move mail. The three keys are parsed at host build, as every other live
/// opt-in is, so a missing one fails the start naming the key rather than the
/// first poll.
/// </summary>
public static class LocalGraphCredential
{
    public const string FeatureKey = "Features:LiveGraphMail";
    public const string TenantIdKey = "Graph:TenantId";
    public const string ClientIdKey = "Graph:ClientId";
    public const string ClientSecretKey = "Graph:ClientSecret";

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
