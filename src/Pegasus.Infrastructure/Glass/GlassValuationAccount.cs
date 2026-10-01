namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The Glass's account Get valuation signs in with: one account Collision
/// Engineers holds for valuations, delivered from Key Vault (ADR-0059). The
/// repair estimate never uses it; each Engineer's own account stays theirs
/// (ADR-0043).
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the password into whatever logged it.
/// </para>
/// </summary>
public sealed class GlassValuationAccount
{
    private GlassValuationAccount(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// A host hands over its own configuration lookup, so which keys the
    /// account needs is decided here. A value the platform left as an
    /// unresolved Key Vault reference is named as that, not as a bad password.
    /// </summary>
    public static GlassValuationAccount Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(
            Require(read, "Glass:ValuationAccount:Username").Trim(),
            // A password is used exactly as it was stored.
            Require(read, "Glass:ValuationAccount:Password"));
    }

    public override string ToString() => nameof(GlassValuationAccount);

    private static string Require(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} is required for Glass's valuation.");
        }

        if (KeyVaultReference.IsUnresolved(value))
        {
            throw new InvalidOperationException(
                $"{key} is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        return value;
    }
}
