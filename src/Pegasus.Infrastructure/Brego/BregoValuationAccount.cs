namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// The Brego valuation account Get valuation signs in with: one account
/// Collision Engineers holds for valuations, delivered from Key Vault
/// (ADR-0084).
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the password into whatever logged it.
/// </para>
/// </summary>
public sealed class BregoValuationAccount
{
    private BregoValuationAccount(string email, string password)
    {
        Email = email;
        Password = password;
    }

    public string Email { get; }

    public string Password { get; }

    /// <summary>
    /// A host hands over its own configuration lookup, so which keys the
    /// account needs is decided here. A value the platform left as an
    /// unresolved Key Vault reference is named as that, not as a bad password.
    /// </summary>
    public static BregoValuationAccount Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(
            Require(read, "Brego:ValuationAccount:Email").Trim(),
            // A password is used exactly as it was stored.
            Require(read, "Brego:ValuationAccount:Password"));
    }

    public override string ToString() => nameof(BregoValuationAccount);

    private static string Require(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value) || HasControlCharacter(value))
        {
            throw new InvalidOperationException($"{key} is required for Brego valuation.");
        }

        if (KeyVaultReference.IsUnresolved(value))
        {
            throw new InvalidOperationException(
                $"{key} is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        return value;
    }

    /// <summary>A plain loop, run for both keys on each press: no enumerator and no delegate.</summary>
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
