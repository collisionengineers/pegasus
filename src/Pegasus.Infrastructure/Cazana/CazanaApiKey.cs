namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// The Cazana API key Get valuation sends: one key Collision Engineers holds,
/// delivered from Key Vault (ADR-0066).
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the key into whatever logged it.
/// </para>
/// </summary>
public sealed class CazanaApiKey
{
    public const string ConfigurationKey = "Cazana:ApiKey";

    private CazanaApiKey(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// A host hands over its own configuration lookup. A value the platform
    /// left as an unresolved Key Vault reference is named as that, not as a
    /// bad key.
    /// </summary>
    public static CazanaApiKey Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        var value = read(ConfigurationKey)?.Trim();
        if (KeyVaultReference.IsUnresolved(value))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey} is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        // A key travels in a header, so it is one unbroken token.
        if (string.IsNullOrEmpty(value) || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new InvalidOperationException($"{ConfigurationKey} is required for Cazana valuation.");
        }

        return new(value);
    }

    public override string ToString() => nameof(CazanaApiKey);
}
