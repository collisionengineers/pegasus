using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// The Glass's account Get valuation signs in with: one account Collision
/// Engineers holds for valuations, held in the database (ADR-0060, ADR-0091). The
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
    private const string LoginName = "Glass's valuation";

    private GlassValuationAccount(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public string Username { get; }

    public string Password { get; }

    /// <summary>
    /// The login as the database holds it (R1288, ADR-0091), read on each
    /// valuation. A login not set, or one the key ring can no longer read, is
    /// named as that, never as a bad password.
    /// </summary>
    internal static GlassValuationAccount Create(IntegrationLoginMaterial? login)
    {
        if (login is null || login.Login != IntegrationLogin.GlassValuation)
        {
            throw new InvalidOperationException("The Glass's valuation login is not set.");
        }

        return new(
            login.Require(IntegrationLoginField.Username, LoginName).Trim(),
            // A password is used exactly as it was stored.
            login.Require(IntegrationLoginField.Password, LoginName));
    }

    public override string ToString() => nameof(GlassValuationAccount);
}
