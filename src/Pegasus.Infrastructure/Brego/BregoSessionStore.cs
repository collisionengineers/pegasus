using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;

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

    /// <summary>
    /// Stores a sign-in Pegasus restored by itself from Brego's e-mail
    /// (R1295) while the account is still at <paramref name="generation"/>,
    /// as an Administrator's link is stored: under the next generation, with
    /// any sign-in waiting for a code dropped and one
    /// <c>integration_login_linked</c> history row, by the system recorder
    /// <c>brego-sign-in</c>. Best effort: <see langword="false"/> when the
    /// account moved since or the store could not be written.
    /// </summary>
    Task<bool> TryStoreRestoredSignInAsync(long generation, BregoStoredSignIn signIn, CancellationToken cancellationToken);
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

/// <summary>
/// Reads the Brego sign-in the Web host kept, until R1288, in its
/// <c>authentication-ring</c> blob container
/// (<c>authentication-ring/brego-session.json</c>) as
/// <c>{ "email", "bregoSession", "refreshToken" }</c>. Only the one-time
/// settings import reads it now, and it goes with the import in the next
/// release. Any storage or parse failure degrades to <see langword="null"/>.
/// </summary>
public sealed class AzureBlobBregoSessionStore(BlobClient blobClient)
{
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(10);

    public async Task<BregoStoredSignIn?> TryLoadAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(IoTimeout);
        try
        {
            var response = await blobClient.DownloadContentAsync(bounded.Token);
            using var document = JsonDocument.Parse(response.Value.Content.ToMemory());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var storedEmail = BregoJson.String(document.RootElement, "email");
            var storedCookie = BregoJson.String(document.RootElement, "bregoSession");
            var storedRefreshToken = BregoJson.String(document.RootElement, "refreshToken");
            if (!string.Equals(storedEmail?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(storedRefreshToken))
            {
                return null;
            }

            return new BregoStoredSignIn(
                string.IsNullOrWhiteSpace(storedCookie) ? null : storedCookie.Trim(),
                storedRefreshToken.Trim());
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
