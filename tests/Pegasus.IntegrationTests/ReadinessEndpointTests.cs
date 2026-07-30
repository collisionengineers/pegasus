using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Deque.AxeCore.Playwright;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class SqlServerReadinessEndpointTests
{
    [Fact]
    public async Task UnavailableSqlKeepsLivenessSuccessfulAndMakesReadinessUnavailable()
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Pegasus"] =
                    "Server=127.0.0.1,1;Database=Pegasus_Unavailable;Integrated Security=true;" +
                    "Encrypt=false;Connect Timeout=1"
            });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PendingSqlMigrationMakesReadinessUnavailable()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        using var factory = SqlServerFactory(database.ConnectionString);
        using var client = CreateClient(factory);

        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
        await using var context = await database.CreateContextAsync();
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'IntakeReceipts', N'IntakeReceiptEvents')"));
    }

    [Fact]
    public async Task MigratedSqlDatabaseMakesReadinessSuccessful()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        using var factory = SqlServerFactory(database.ConnectionString);
        using var client = CreateClient(factory);

        using var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnhealthyResponseDoesNotExposeDatabaseOrConnectionDetails()
    {
        const string databaseName = "Pegasus_SecretDatabaseName";
        const string applicationName = "Pegasus_Readiness_NoLeak";
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Pegasus"] =
                    $"Server=127.0.0.1,1;Database={databaseName};Application Name={applicationName};" +
                    "Integrated Security=true;" +
                    "Encrypt=false;Connect Timeout=1"
            });
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", body);
        Assert.DoesNotContain(databaseName, body, StringComparison.Ordinal);
        Assert.DoesNotContain(applicationName, body, StringComparison.Ordinal);
        Assert.DoesNotContain("SqlException", body, StringComparison.Ordinal);
    }

    private static ConfiguredWebApplicationFactory SqlServerFactory(string connectionString) => new(
        "Production",
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:Pegasus"] = connectionString
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
}

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class LocalDbReadinessEndpointTests
{
    [Fact]
    public async Task DevelopmentLocalDbMakesReadinessSuccessful()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DevelopmentStartupDoesNotApplyMigrations()
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.StartupMigrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
            using var factory = new ConfiguredWebApplicationFactory(
                "Development",
                new Dictionary<string, string?>
                {
                    ["Runtime:Profile"] = "DevelopmentOffline",
                    ["ConnectionStrings:Pegasus"] = database.ConnectionString,
                    ["Intake:LocalArtifactPath"] = Path.Combine(workingDirectory, "intake"),
                    ["Features:LocalIntake"] = "false"
                });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

            using var liveResponse = await client.GetAsync("/health/live");
            using var readyResponse = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
            Assert.Equal("Healthy", await liveResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
            Assert.Equal("Unhealthy", await readyResponse.Content.ReadAsStringAsync());
            Assert.Equal(
                0,
                await database.ScalarAsync<int>(
                    "SELECT COUNT(*) FROM sys.tables WHERE name = N'__EFMigrationsHistory'"));
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }
}

[Trait("Category", "Browser")]
public sealed class OfflineBrowserReadinessTests
{
    [Fact]
    public async Task PinnedChromiumAndAxeRunWithoutNetworkDependency()
    {
        const string readinessDocument =
            """
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Pegasus offline browser readiness</title>
            </head>
            <body>
                <main>
                    <h1>Browser dependencies ready</h1>
                </main>
            </body>
            </html>
            """;

        var violationIds = await OfflineBrowserAxe.FindViolationIdsAsync(readinessDocument);

        Assert.Empty(violationIds);
    }
}

internal static class OfflineBrowserAxe
{
    public static async Task<IReadOnlyList<string>> FindViolationIdsAsync(string html)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true
            });
        var page = await browser.NewPageAsync(
            new BrowserNewPageOptions
            {
                ColorScheme = ColorScheme.Light,
                ReducedMotion = ReducedMotion.Reduce,
                ViewportSize = new ViewportSize
                {
                    Width = 1280,
                    Height = 720
                }
            });

        await page.SetContentAsync(
            html,
            new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });
        var axeResult = await page.RunAxe();

        return axeResult.Violations?
            .Select(violation => violation.Id)
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
    }
}

internal sealed class ConfiguredWebApplicationFactory(
    string environment,
    IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    private readonly X509Certificate2 encryptionCertificate = CreateDevelopmentCertificate(
        "Pegasus integration test encryption",
        X509KeyUsageFlags.KeyEncipherment);
    private readonly X509Certificate2 signingCertificate = CreateDevelopmentCertificate(
        "Pegasus integration test signing",
        X509KeyUsageFlags.DigitalSignature);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(settings));
        builder.ConfigureServices(services =>
        {
            services.AddOpenIddict()
                .AddServer(options => options
                    .AddEncryptionCertificate(encryptionCertificate)
                    .AddSigningCertificate(signingCertificate));
        });
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing)
            {
                encryptionCertificate.Dispose();
                signingCertificate.Dispose();
            }
        }
    }

    private static X509Certificate2 CreateDevelopmentCertificate(
        string commonName,
        X509KeyUsageFlags keyUsage)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, critical: true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
    }
}
