using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Experian;

/// <summary>
/// One Experian Automotive API account and where it signs in: Collision
/// Engineers' client and user credentials, held in the database (R1288,
/// ADR-0091), and the two endpoint addresses, which are settings (ADR-0089).
/// Experian issues an account per permission, so each act is handed its own:
/// the history login holds condition records for Run Experian check and the
/// CAP login holds CAP valuations (R1207, R1215). The addresses are shared.
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the secrets into whatever logged it.
/// </para>
/// </summary>
public sealed class ExperianAccount
{
    private ExperianAccount(
        long generation,
        Uri baseUri,
        Uri tokenUri,
        string clientId,
        string clientSecret,
        string username,
        string password)
    {
        Generation = generation;
        BaseUri = baseUri;
        TokenUri = tokenUri;
        ClientId = clientId;
        ClientSecret = clientSecret;
        Username = username;
        Password = password;
    }

    /// <summary>The login's generation: a token signed in under another is not reused.</summary>
    public long Generation { get; }

    /// <summary>The API root, for example <c>https://sandbox-uk-api.experian.com/ukci/automotive</c>.</summary>
    public Uri BaseUri { get; }

    /// <summary>The OAuth2 token endpoint, for example <c>https://sandbox-uk-api.experian.com/oauth2/v1/token</c>.</summary>
    public Uri TokenUri { get; }

    public string ClientId { get; }

    public string ClientSecret { get; }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// The login as the database holds it (R1288, ADR-0091), read on each
    /// call, and the two addresses from the host's configuration lookup. A
    /// login not set, or one the key ring can no longer read, is named as
    /// that, never as a bad secret. Both addresses are HTTPS on an Experian
    /// host.
    /// </summary>
    internal static ExperianAccount Create(IntegrationLoginMaterial? login, Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (login is null
            || login.Login is not (IntegrationLogin.ExperianHistory or IntegrationLogin.ExperianCap))
        {
            throw new InvalidOperationException("The Experian login is not set.");
        }

        var loginName = login.Login == IntegrationLogin.ExperianHistory ? "Experian history" : "Experian CAP";
        return new(
            login.Generation,
            ExperianUri(read, "Experian:BaseUri"),
            ExperianUri(read, "Experian:TokenUri"),
            login.Require(IntegrationLoginField.ClientId, loginName).Trim(),
            login.Require(IntegrationLoginField.ClientSecret, loginName),
            login.Require(IntegrationLoginField.Username, loginName).Trim(),
            // A password is used exactly as it was stored.
            login.Require(IntegrationLoginField.Password, loginName));
    }

    public override string ToString() => nameof(ExperianAccount);

    private static Uri ExperianUri(Func<string, string?> read, string key)
    {
        var value = Setting(read, key).Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !uri.Host.EndsWith(".experian.com", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException($"{key} must be an HTTPS Experian address.");
        }

        return uri;
    }

    private static string Setting(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value) || IntegrationLoginMaterial.HasControlCharacter(value))
        {
            throw new InvalidOperationException($"{key} is required for the Experian check.");
        }

        return value;
    }
}
