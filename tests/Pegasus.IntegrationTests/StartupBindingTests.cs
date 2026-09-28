using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Server;
using Pegasus.Web.Health;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Nothing that waits on a managed-identity token may hold the port. These tests
/// pin the pieces that keep the remote reads (the key ring, the Automation
/// OAuth certificates, the verification account) behind the listening port.
/// </summary>
public sealed class StartupBindingTests
{
    private const string KeyRingLoader =
        "Microsoft.AspNetCore.DataProtection.Internal.DataProtectionHostedService";

    [Fact]
    public void TheFrameworkKeyRingLoadHoldsThePortUntilItIsDeferred()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        // If the framework stops adding it, this assertion fails and the
        // deferral can be deleted.
        Assert.Contains(services, IsKeyRingLoader);
        services.DeferDataProtectionKeyRingLoad();

        Assert.DoesNotContain(services, IsKeyRingLoader);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IDataProtectionProvider>());
    }

    [Fact]
    public void ADeferredKeyRingIsOnlyDeferredByTheLastCall()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.DeferDataProtectionKeyRingLoad();

        // Another AddDataProtection adds the loader back, which is why
        // Program.cs defers just before Build.
        services.AddDataProtection();
        Assert.Contains(services, IsKeyRingLoader);
        services.DeferDataProtectionKeyRingLoad();
        Assert.DoesNotContain(services, IsKeyRingLoader);
    }

    [Fact]
    public async Task TheCertificateLoadNeverHoldsTheStartAndRetriesUntilItSucceeds()
    {
        var store = new OAuthCertificateStore();
        using var release = new ManualResetEventSlim();
        var attempts = 0;
        OAuthCertificateSet Load()
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                // The managed-identity sidecar has not answered yet.
                release.Wait(TimeSpan.FromSeconds(30));
                throw new InvalidOperationException("The sidecar is not ready.");
            }

            return new OAuthCertificateSet([], []);
        }

        using var service = new OAuthCertificateLoadService(
            store,
            Load,
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            NullLogger<OAuthCertificateLoadService>.Instance);

        // StartAsync returns while the first attempt is still blocked.
        await service.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(store.IsReady);

        release.Set();
        await store.Loaded.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(store.IsReady);
        Assert.Equal(2, attempts);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task TheGateRefusesEverythingButProbesUntilTheCertificatesLoad()
    {
        var store = new OAuthCertificateStore();
        var reached = new List<string>();
        RequestDelegate next = context =>
        {
            reached.Add(context.Request.Path);
            return Task.CompletedTask;
        };

        var refused = Context("/Cases");
        await store.Gate(refused, next);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, refused.Response.StatusCode);
        Assert.Equal("5", refused.Response.Headers.RetryAfter.ToString());
        Assert.Empty(reached);

        await store.Gate(Context("/health/warm"), next);
        await store.Gate(Context("/diagnostics/version"), next);
        Assert.Equal("/health/warm,/diagnostics/version", string.Join(',', reached));

        store.Complete(new OAuthCertificateSet([], []));
        await store.Gate(Context("/Cases"), next);
        Assert.Equal("/Cases", reached[^1]);
    }

    [Fact]
    public void CertificatesJoinTheTokenServerOptionsOnlyOnceLoaded()
    {
        using var signing = Certificate("startup-signing");
        using var encryption = Certificate("startup-encryption");
        var store = new OAuthCertificateStore();

        Assert.Throws<InvalidOperationException>(() => store.AddTo(new OpenIddictServerOptions()));

        store.Complete(new OAuthCertificateSet([signing], [encryption]));
        var options = new OpenIddictServerOptions();
        store.AddTo(options);

        Assert.Single(options.SigningCredentials);
        Assert.Single(options.EncryptionCredentials);
    }

    [Fact]
    public void ThePhaseTimelineCountsTheTimeBeforeTheFirstMark()
    {
        var lines = new List<string>();
        var timeline = new StartupTimeline(lines.Add, TimeSpan.FromSeconds(2));

        timeline.Mark("host built");

        Assert.Matches(@"^\[startup\] \+\d{4,} ms \(\+\d{4,} ms\) host built$", Assert.Single(lines));
        Assert.Matches(@"^host built \+\d{4,} ms \(\+\d{4,} ms\)$", timeline.Summary());
    }

    private static bool IsKeyRingLoader(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationType?.FullName == KeyRingLoader;

    private static DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    private static X509Certificate2 Certificate(string name)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var generated = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            generated.Export(X509ContentType.Pkcs12),
            null,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }
}
