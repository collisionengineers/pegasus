using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                ["Database:Provider"] = "SqlServer",
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
                ["Database:Provider"] = "SqlServer",
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
            ["Database:Provider"] = "SqlServer",
            ["ConnectionStrings:Pegasus"] = connectionString
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
}

public sealed class SqliteReadinessEndpointTests
{
    [Fact]
    public async Task DevelopmentSqliteDatabaseMakesReadinessSuccessful()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DevelopmentStartupDoesNotApplySqliteMigrations()
    {
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.StartupMigrationTests",
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(workingDirectory, "startup.db");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            using var factory = new ConfiguredWebApplicationFactory(
                "Development",
                new Dictionary<string, string?>
                {
                    ["Runtime:Profile"] = "DevelopmentOffline",
                    ["Database:Provider"] = "Sqlite",
                    ["Database:LocalPath"] = databasePath,
                    ["Intake:LocalArtifactPath"] = Path.Combine(workingDirectory, "intake"),
                    ["Features:LocalIntake"] = "false"
                });
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });

            using var response = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            await using var connection = new SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory'";
            Assert.Equal(0L, Convert.ToInt64(
                await command.ExecuteScalarAsync(),
                System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(workingDirectory, recursive: true);
        }
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
