namespace Pegasus.Infrastructure;

/// <summary>
/// During provisioning App Service can hand the app the literal
/// <c>@Microsoft.KeyVault(...)</c> placeholder instead of the secret it names.
/// An adapter that reads a Key Vault-held setting names that state directly,
/// so it is never mistaken for a malformed secret; the Web's one-time import of
/// the integrations' logins skips one (R1288).
/// </summary>
public static class KeyVaultReference
{
    public static bool IsUnresolved(string? value) =>
        value is not null
        && value.TrimStart().StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase);
}
