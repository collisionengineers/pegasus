using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Keeps the Brego valuation account signed in across Web host restarts
/// (ADR-0084): the Cognito refresh token Pegasus renews its 24-hour tokens
/// from, and the 1-year <c>brego-session</c> cookie, so Brego's browser
/// device-approval challenge is not re-triggered on container recycles or
/// subsequent days. The account is linked once by
/// <c>scripts/Connect-BregoAccount.ps1</c>, which writes the first record.
/// </summary>
public interface IBregoSessionStore
{
    /// <summary>
    /// The stored sign-in for <paramref name="email"/>, or <see langword="null"/>
    /// when none is stored for it, it holds no refresh token, or it cannot be read.
    /// </summary>
    Task<BregoStoredSignIn?> TryLoadAsync(string email, CancellationToken cancellationToken);

    /// <summary>Stores <paramref name="signIn"/> for <paramref name="email"/>; best effort.</summary>
    Task TrySaveAsync(string email, BregoStoredSignIn signIn, CancellationToken cancellationToken);
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
/// Stores the Brego sign-in in the Web host's <c>authentication-ring</c> blob
/// container (<c>authentication-ring/brego-session.json</c>) as
/// <c>{ "email", "bregoSession", "refreshToken" }</c>. Any storage or parse
/// failure degrades to <see langword="null"/> or a no-op so a transient blob
/// error never aborts a valuation.
/// </summary>
public sealed class AzureBlobBregoSessionStore(BlobClient blobClient) : IBregoSessionStore
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

    public async Task TrySaveAsync(
        string email,
        BregoStoredSignIn signIn,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)
            || signIn is null
            || string.IsNullOrWhiteSpace(signIn.RefreshToken))
        {
            return;
        }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(IoTimeout);
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                email = email.Trim(),
                bregoSession = string.IsNullOrWhiteSpace(signIn.BregoSessionCookie)
                    ? null
                    : signIn.BregoSessionCookie.Trim(),
                refreshToken = signIn.RefreshToken.Trim(),
            });
            await blobClient.UploadAsync(BinaryData.FromBytes(payload), overwrite: true, bounded.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Best-effort persistence: in-memory reuse still serves the running host.
        }
    }
}
