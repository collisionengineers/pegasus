using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Persists the 1-year <c>brego-session</c> cookie across Web host restarts and
/// 24-hour Cognito token refreshes so Brego's browser device-approval challenge
/// is not re-triggered on container recycles or subsequent days (ADR-0084).
/// </summary>
public interface IBregoSessionStore
{
    Task<string?> TryLoadSessionCookieAsync(string email, CancellationToken cancellationToken);

    Task TrySaveSessionCookieAsync(string email, string bregoSessionCookie, CancellationToken cancellationToken);
}

/// <summary>
/// Stores the active <c>brego-session</c> cookie in the Web host's
/// <c>authentication-ring</c> blob container (<c>authentication-ring/brego-session.json</c>).
/// Any storage or parse failure degrades to <see langword="null"/> or a no-op so
/// a transient blob error never aborts a valuation.
/// </summary>
public sealed class AzureBlobBregoSessionStore(BlobClient blobClient) : IBregoSessionStore
{
    private static readonly TimeSpan IoTimeout = TimeSpan.FromSeconds(10);

    public async Task<string?> TryLoadSessionCookieAsync(string email, CancellationToken cancellationToken)
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
            if (!string.Equals(storedEmail, email.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(storedCookie))
            {
                return null;
            }

            return storedCookie.Trim();
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

    public async Task TrySaveSessionCookieAsync(
        string email,
        string bregoSessionCookie,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(bregoSessionCookie))
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
                bregoSession = bregoSessionCookie.Trim(),
            });
            await blobClient.UploadAsync(BinaryData.FromBytes(payload), overwrite: true, bounded.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Best-effort persistence: in-memory reuse still serves the running host.
        }
    }
}
