using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// The Brego valuation account Get valuation signs in with: one account
/// Collision Engineers holds for valuations, named by its e-mail address and
/// held in the database with its stored sign-in (ADR-0084, ADR-0091). Pegasus
/// keeps itself signed in from the stored Cognito refresh token and
/// <c>brego-session</c> cookie, renewed through <see cref="IBregoSessionStore"/>.
/// The account is linked by its e-mailed sign-in link on the Integrations
/// page, and may also hold a password: with one, Pegasus signs in with it
/// whenever it holds no usable sign-in (R1290).
///
/// <para>
/// A class rather than a record on purpose: a record's generated
/// <c>ToString</c> would print the refresh token into whatever logged it.
/// </para>
/// </summary>
public sealed class BregoValuationAccount
{
    private const string LoginName = "Brego valuation";

    private BregoValuationAccount(
        string email,
        string? password,
        string? refreshToken,
        string? sessionCookie,
        long generation)
    {
        Email = email;
        Password = password;
        RefreshToken = refreshToken;
        SessionCookie = sessionCookie;
        Generation = generation;
    }

    public string Email { get; }

    /// <summary>The saved password, kept exactly as entered; none when the account signs in by link alone.</summary>
    public string? Password { get; }

    /// <summary>The stored Cognito refresh token; none until the account is linked.</summary>
    public string? RefreshToken { get; }

    /// <summary>The stored <c>brego-session</c> cookie, once one has been minted.</summary>
    public string? SessionCookie { get; }

    /// <summary>
    /// The login's generation: it moves when an Administrator replaces the
    /// account or signs it in on the Integrations page, never when a token is
    /// renewed, so a session signed in under another generation is not reused.
    /// </summary>
    public long Generation { get; }

    /// <summary>
    /// The login as the database holds it (R1288, ADR-0091), read on each
    /// valuation. A login not set, or one the key ring can no longer read, is
    /// named as that, never as a bad address.
    /// </summary>
    internal static BregoValuationAccount Create(IntegrationLoginMaterial? login)
    {
        if (login is null || login.Login != IntegrationLogin.Brego)
        {
            throw new InvalidOperationException("The Brego valuation login is not set.");
        }

        return new(
            login.Require(IntegrationLoginField.Email, LoginName).Trim(),
            string.IsNullOrWhiteSpace(login.Value(IntegrationLoginField.Password))
                ? null
                : login.Value(IntegrationLoginField.Password),
            Usable(login.RefreshToken),
            Usable(login.SessionCookie),
            login.Generation);
    }

    public override string ToString() => nameof(BregoValuationAccount);

    private static string? Usable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
