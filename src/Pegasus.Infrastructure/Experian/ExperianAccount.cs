namespace Pegasus.Infrastructure.Experian;

/// <summary>
/// The Experian Automotive API account Run Experian check signs in with, and
/// where it signs in: Collision Engineers' client and user credentials,
/// delivered from Key Vault, and the two endpoint addresses (ADR-0089).
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the secrets into whatever logged it.
/// </para>
/// </summary>
public sealed class ExperianAccount
{
    private ExperianAccount(
        Uri baseUri,
        Uri tokenUri,
        string clientId,
        string clientSecret,
        string username,
        string password)
    {
        BaseUri = baseUri;
        TokenUri = tokenUri;
        ClientId = clientId;
        ClientSecret = clientSecret;
        Username = username;
        Password = password;
    }

    /// <summary>The API root, for example <c>https://sandbox-uk-api.experian.com/ukci/automotive</c>.</summary>
    public Uri BaseUri { get; }

    /// <summary>The OAuth2 token endpoint, for example <c>https://sandbox-uk-api.experian.com/oauth2/v1/token</c>.</summary>
    public Uri TokenUri { get; }

    public string ClientId { get; }

    public string ClientSecret { get; }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// A host hands over its own configuration lookup, so which keys the
    /// account needs is decided here. A value the platform left as an
    /// unresolved Key Vault reference is named as that, not as a bad secret.
    /// Both addresses are HTTPS on an Experian host.
    /// </summary>
    public static ExperianAccount Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(
            ExperianUri(read, "Experian:BaseUri"),
            ExperianUri(read, "Experian:TokenUri"),
            Require(read, "Experian:Account:ClientId").Trim(),
            Require(read, "Experian:Account:ClientSecret"),
            Require(read, "Experian:Account:Username").Trim(),
            // A password is used exactly as it was stored.
            Require(read, "Experian:Account:Password"));
    }

    public override string ToString() => nameof(ExperianAccount);

    private static Uri ExperianUri(Func<string, string?> read, string key)
    {
        var value = Require(read, key).Trim();
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

    private static string Require(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value) || HasControlCharacter(value))
        {
            throw new InvalidOperationException($"{key} is required for the Experian check.");
        }

        if (KeyVaultReference.IsUnresolved(value))
        {
            throw new InvalidOperationException(
                $"{key} is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        return value;
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
