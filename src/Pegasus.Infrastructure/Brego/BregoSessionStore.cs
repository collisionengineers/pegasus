namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Keeps the Brego valuation account signed in across Web host restarts
/// (ADR-0084): the Cognito refresh token Pegasus renews its 24-hour tokens
/// from, and the 1-year <c>brego-session</c> cookie, so Brego's browser
/// device-approval challenge is not re-triggered on container recycles or
/// subsequent days. The stored sign-in is read with the account itself, in
/// the database beside its e-mail address (R1288, ADR-0091); this port only
/// writes back what a renewal changed.
/// </summary>
public interface IBregoSessionStore
{
    /// <summary>
    /// Stores <paramref name="signIn"/> for the account at
    /// <paramref name="generation"/>, without counting as a change to the
    /// login. Best effort: <see langword="false"/> when the account was
    /// replaced since (its generation moved) or the store could not be written.
    /// </summary>
    Task<bool> TrySaveAsync(long generation, BregoStoredSignIn signIn, CancellationToken cancellationToken);
}

/// <summary>
/// A stored Brego sign-in: the Cognito refresh token and, once one has been
/// minted, the <c>brego-session</c> cookie. Its <c>ToString</c> prints
/// neither.
/// </summary>
public sealed record BregoStoredSignIn(string? BregoSessionCookie, string RefreshToken)
{
    public override string ToString() => nameof(BregoStoredSignIn);
}
