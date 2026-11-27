using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// The Cazana Companion account Get valuation signs in with: one account
/// Collision Engineers holds for valuations, held in the database
/// (ADR-0079, ADR-0091).
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the password into whatever logged it.
/// </para>
/// </summary>
public sealed class CazanaCompanionAccount
{
    private const string LoginName = "Cazana Companion";

    private CazanaCompanionAccount(string email, string password)
    {
        Email = email;
        Password = password;
    }

    public string Email { get; }

    public string Password { get; }

    /// <summary>
    /// The login as the database holds it (R1288, ADR-0091), read on each
    /// valuation. A login not set, or one the key ring can no longer read, is
    /// named as that, never as a bad password.
    /// </summary>
    internal static CazanaCompanionAccount Create(IntegrationLoginMaterial? login)
    {
        if (login is null || login.Login != IntegrationLogin.Cazana)
        {
            throw new InvalidOperationException("The Cazana Companion login is not set.");
        }

        return new(
            login.Require(IntegrationLoginField.Email, LoginName).Trim(),
            // A password is used exactly as it was stored.
            login.Require(IntegrationLoginField.Password, LoginName));
    }

    public override string ToString() => nameof(CazanaCompanionAccount);
}
