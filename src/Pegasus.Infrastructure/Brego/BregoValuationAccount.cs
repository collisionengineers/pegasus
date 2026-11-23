namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// The Brego valuation account Get valuation signs in with: one account
/// Collision Engineers holds for valuations, named by its e-mail address and
/// delivered from Key Vault (ADR-0084). Brego signs the account in by e-mailed
/// magic link only, so the account holds no password: it is linked once by
/// <c>scripts/Connect-BregoAccount.ps1</c>, and Pegasus keeps itself signed in
/// from the stored Cognito refresh token (<see cref="IBregoSessionStore"/>).
/// </summary>
public sealed class BregoValuationAccount
{
    private const string EmailKey = "Brego:ValuationAccount:Email";

    private BregoValuationAccount(string email)
    {
        Email = email;
    }

    public string Email { get; }

    /// <summary>
    /// A host hands over its own configuration lookup, so which key the
    /// account needs is decided here. A value the platform left as an
    /// unresolved Key Vault reference is named as that, not as a bad address.
    /// </summary>
    public static BregoValuationAccount Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var value = read(EmailKey);
        if (string.IsNullOrWhiteSpace(value) || HasControlCharacter(value))
        {
            throw new InvalidOperationException($"{EmailKey} is required for Brego valuation.");
        }

        if (KeyVaultReference.IsUnresolved(value))
        {
            throw new InvalidOperationException(
                $"{EmailKey} is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        return new(value.Trim());
    }

    public override string ToString() => nameof(BregoValuationAccount);

    /// <summary>A plain loop, run on each press: no enumerator and no delegate.</summary>
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
