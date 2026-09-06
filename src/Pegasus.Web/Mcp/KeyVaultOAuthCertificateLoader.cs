using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Core;

namespace Pegasus.Web.Mcp;

internal sealed record OAuthCertificateSet(
    IReadOnlyList<X509Certificate2> Signing,
    IReadOnlyList<X509Certificate2> Encryption);

internal static class KeyVaultOAuthCertificateLoader
{
    private const int MaximumBytes = 1024 * 1024;
    private static readonly string[] Scope = ["https://vault.azure.net/.default"];
    private static readonly HttpClient Client = new(
        new SocketsHttpHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static OAuthCertificateSet Load(
        TokenCredential credential,
        IReadOnlyList<Uri> signing,
        IReadOnlyList<Uri> encryption) =>
        Load(credential, Client, signing, encryption);

    internal static OAuthCertificateSet Load(
        TokenCredential credential,
        HttpClient client,
        IReadOnlyList<Uri> signing,
        IReadOnlyList<Uri> encryption)
    {
        var loaded = new List<X509Certificate2>();
        try
        {
            var signingCertificates = LoadPurpose(credential, client, signing, "signing", loaded);
            var encryptionCertificates = LoadPurpose(credential, client, encryption, "encryption", loaded);
            var signingThumbprints = signingCertificates
                .Select(value => value.Thumbprint)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (encryptionCertificates.Any(value => signingThumbprints.Contains(value.Thumbprint)))
            {
                throw new InvalidOperationException(
                    "Automation OAuth signing and encryption require distinct certificates.");
            }
            return new(signingCertificates, encryptionCertificates);
        }
        catch
        {
            foreach (var certificate in loaded) certificate.Dispose();
            throw;
        }
    }

    private static List<X509Certificate2> LoadPurpose(
        TokenCredential credential,
        HttpClient client,
        IReadOnlyList<Uri> uris,
        string purpose,
        List<X509Certificate2> loaded)
    {
        var certificates = new List<X509Certificate2>(uris.Count);
        foreach (var uri in uris)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var token = credential.GetToken(new TokenRequestContext(Scope), timeout.Token);
            using var request = new HttpRequestMessage(
                HttpMethod.Get, new UriBuilder(uri) { Query = "api-version=7.4" }.Uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"The Automation OAuth {purpose} certificate could not be loaded ({(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength > MaximumBytes)
                throw new InvalidOperationException(
                    $"The Automation OAuth {purpose} certificate response is too large.");

            using var source = response.Content.ReadAsStream(timeout.Token);
            using var bounded = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = source.ReadAsync(buffer, timeout.Token).AsTask().GetAwaiter().GetResult()) != 0)
            {
                if (bounded.Length + read > MaximumBytes)
                    throw new InvalidOperationException(
                        $"The Automation OAuth {purpose} certificate response is too large.");
                bounded.Write(buffer, 0, read);
            }
            bounded.Position = 0;
            using var json = JsonDocument.Parse(bounded);
            try
            {
                var encoded = json.RootElement.GetProperty("value").GetString();
                var certificate = X509CertificateLoader.LoadPkcs12(
                    Convert.FromBase64String(encoded ?? string.Empty),
                    null,
                    X509KeyStorageFlags.EphemeralKeySet);
                if (!certificate.HasPrivateKey)
                {
                    certificate.Dispose();
                    throw new InvalidOperationException(
                        $"The Automation OAuth {purpose} certificate has no private key.");
                }
                certificates.Add(certificate);
                loaded.Add(certificate);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                throw new InvalidOperationException(
                    $"The Automation OAuth {purpose} secret is not a passwordless PFX.", exception);
            }
        }
        return certificates;
    }
}
