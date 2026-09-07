using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Azure.Core;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

public sealed class AutomationOAuthCertificateLoaderTests
{
    [Fact]
    public void ReplicaLoadsResolveTheSamePersistentKeys()
    {
        using var signing = Certificate("persistent-signing");
        using var encryption = Certificate("persistent-encryption");
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["/secrets/signing/version"] = signing.Export(X509ContentType.Pkcs12),
            ["/secrets/encryption/version"] = encryption.Export(X509ContentType.Pkcs12)
        };
        var signingUri = new Uri("https://pegasus.vault.azure.net/secrets/signing/version");
        var encryptionUri = new Uri("https://pegasus.vault.azure.net/secrets/encryption/version");
        using var firstClient = new HttpClient(new SecretHandler(payloads));
        using var secondClient = new HttpClient(new SecretHandler(payloads));
        var first = KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), firstClient, [signingUri], [encryptionUri]);
        var second = KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), secondClient, [signingUri], [encryptionUri]);
        try
        {
            Assert.Equal(first.Signing[0].Thumbprint, second.Signing[0].Thumbprint);
            Assert.Equal(first.Encryption[0].Thumbprint, second.Encryption[0].Thumbprint);
            var tokenPayload = "issued-by-first-replica"u8.ToArray();
            using var issuer = first.Signing[0].GetRSAPrivateKey();
            using var verifier = second.Signing[0].GetRSAPublicKey();
            var signature = issuer!.SignData(tokenPayload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.True(verifier!.VerifyData(tokenPayload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }
        finally
        {
            foreach (var certificate in first.Signing.Concat(first.Encryption).Concat(second.Signing).Concat(second.Encryption)) certificate.Dispose();
        }
    }

    [Fact]
    public void LoadsRotationOverlapThroughCredentialAndHttpBoundary()
    {
        using var signingCurrent = Certificate("signing-current");
        using var signingNext = Certificate("signing-next");
        using var encryptionCurrent = Certificate("encryption-current");
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["/secrets/signing/current"] = signingCurrent.Export(X509ContentType.Pkcs12),
            ["/secrets/signing/next"] = signingNext.Export(X509ContentType.Pkcs12),
            ["/secrets/encryption/current"] = encryptionCurrent.Export(X509ContentType.Pkcs12)
        };
        using var client = new HttpClient(new SecretHandler(payloads));
        var credential = new FixedCredential();

        var result = KeyVaultOAuthCertificateLoader.Load(
            credential,
            client,
            [new("https://pegasus.vault.azure.net/secrets/signing/current"), new("https://pegasus.vault.azure.net/secrets/signing/next")],
            [new("https://pegasus.vault.azure.net/secrets/encryption/current")]);
        try
        {
            Assert.Equal(2, result.Signing.Count);
            Assert.Single(result.Encryption);
            Assert.Equal(3, credential.Requests);
            Assert.All(result.Signing.Concat(result.Encryption), certificate => Assert.True(certificate.HasPrivateKey));
        }
        finally
        {
            foreach (var certificate in result.Signing.Concat(result.Encryption)) certificate.Dispose();
        }
    }

    [Fact]
    public void RejectsCertificateReusedAcrossPurposes()
    {
        using var certificate = Certificate("shared");
        using var client = ClientFor(certificate);
        var uri = new Uri("https://pegasus.vault.azure.net/secrets/shared/version");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), client, [uri], [uri]));

        Assert.Contains("distinct certificates", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RedirectIsNotFollowedAndFailsClosed()
    {
        using var client = new HttpClient(new RedirectHandler());
        var uri = new Uri("https://pegasus.vault.azure.net/secrets/signing/version");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), client, [uri], [uri]));

        Assert.Contains("(302)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnboundedSecretResponseFailsClosed()
    {
        using var client = new HttpClient(new OversizedHandler());
        var uri = new Uri("https://pegasus.vault.azure.net/secrets/signing/version");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), client, [uri], [uri]));

        Assert.Contains("too large", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOrInvalidSecretFailsClosed()
    {
        var uri = new Uri("https://pegasus.vault.azure.net/secrets/signing/version");
        using (var missing = new HttpClient(new StatusHandler(HttpStatusCode.NotFound)))
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), missing, [uri], [uri]));
            Assert.Contains("(404)", exception.Message, StringComparison.Ordinal);
        }
        using (var invalid = new HttpClient(new JsonHandler("{\"value\":\"not-a-pfx\"}")))
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                KeyVaultOAuthCertificateLoader.Load(new FixedCredential(), invalid, [uri], [uri]));
            Assert.Contains("not a passwordless PFX", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task CertificateResponseBodyCannotOutliveTheStartupRequestDeadline()
    {
        using var client = new HttpClient(new StalledBodyHandler());
        var uri = new Uri("https://pegasus.vault.azure.net/secrets/signing/version");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await Task.Run(() => KeyVaultOAuthCertificateLoader.Load(
                    new FixedCredential(), client, [uri], [uri]))
                .WaitAsync(TimeSpan.FromSeconds(25)));
    }

    private static HttpClient ClientFor(X509Certificate2 certificate) =>
        new(new SecretHandler(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["/secrets/shared/version"] = certificate.Export(X509ContentType.Pkcs12)
        }));

    private static X509Certificate2 Certificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pkcs12), null,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private sealed class FixedCredential : TokenCredential
    {
        public int Requests { get; private set; }
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("https://vault.azure.net/.default", Assert.Single(requestContext.Scopes));
            return new("test-token", DateTimeOffset.UtcNow.AddMinutes(5));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class SecretHandler(IReadOnlyDictionary<string, byte[]> payloads) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
            Assert.Equal("api-version=7.4", request.RequestUri?.Query.TrimStart('?'));
            var bytes = payloads[request.RequestUri!.AbsolutePath];
            var json = $"{{\"value\":\"{Convert.ToBase64String(bytes)}\"}}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.Redirect)
            {
                Headers = { Location = new Uri("https://evil.example/secret") }
            };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class OversizedHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[1024 * 1024 + 1])
            };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => new(status);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class StalledBodyHandler : HttpMessageHandler
    {
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
            new(HttpStatusCode.OK) { Content = new StreamContent(new StalledBody()) };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Send(request, cancellationToken));
    }

    private sealed class StalledBody : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
