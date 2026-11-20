using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The Glass's login the Automation actor's own Glass's sessions sign in with
/// (operator, 18 November 2026; R1177): a login the deployment holds,
/// delivered as two secrets, never a member of staff's stored credential, so
/// Pegasus never decrypts a staff login for the Automation (design 07
/// section 1). Its account key comes from <see cref="GlassAccountKey"/>, the
/// rule a staff login's comes from, so the same login configured for both
/// is one live session's slot.
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the password into whatever logged it.
/// </para>
/// </summary>
public sealed class GlassAutomationAccount
{
    public const string UsernameKey = "Glass:AutomationAccount:Username";

    public const string PasswordKey = "Glass:AutomationAccount:Password";

    private GlassAutomationAccount(string username, string password)
    {
        Username = username;
        Password = password;
        AccountKey = GlassAccountKey.Of(username);
    }

    public string Username { get; }

    public string Password { get; }

    /// <summary>The login's account key; the username itself is never recorded on a session.</summary>
    public string AccountKey { get; }

    /// <summary>
    /// A host hands over its own configuration lookup, so which keys the
    /// account needs is decided here. A value the platform left as an
    /// unresolved Key Vault reference is named as that, not as a bad password.
    /// </summary>
    public static GlassAutomationAccount Create(Func<string, string?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(
            Require(read, UsernameKey).Trim(),
            // A password is used exactly as it was stored.
            Require(read, PasswordKey));
    }

    public override string ToString() => nameof(GlassAutomationAccount);

    private static string Require(Func<string, string?> read, string key)
    {
        var value = read(key);
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
        {
            throw new InvalidOperationException($"{key} is required for the Automation's Glass's sessions.");
        }

        if (KeyVaultReference.IsUnresolved(value))
        {
            throw new InvalidOperationException(
                $"{key} is an unresolved Key Vault reference; the platform has not resolved the secret.");
        }

        return value;
    }
}

/// <summary>
/// Reads <see cref="GlassAutomationAccount"/> on each use, as the valuation
/// account is read, because the platform can hand over an unresolved Key
/// Vault reference, which must not fail the host at build. Only an act the
/// Automation runs reads it.
/// </summary>
public sealed class GlassAutomationAccountSource(Func<GlassAutomationAccount> read)
{
    public GlassAutomationAccount Read() => read();

    public override string ToString() => nameof(GlassAutomationAccountSource);
}
