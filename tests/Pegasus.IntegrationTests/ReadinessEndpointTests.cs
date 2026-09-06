using System.Net;
using Deque.AxeCore.Playwright;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

public sealed class WebCompositionTests
{
    [Theory]
    [InlineData("Production", typeof(AzureBlobIntakeArtifactStore))]
    [InlineData("Development", typeof(FileSystemIntakeArtifactStore))]
    public void RuntimeProfilesResolveOperationsSnapshotWithTheirIntendedArtifactStore(
        string environment,
        Type expectedArtifactStoreType)
    {
        var settings = environment.Equals("Development", StringComparison.Ordinal)
            ? new Dictionary<string, string?>
            {
                ["Intake:LocalArtifactPath"] = Path.Combine(
                    Path.GetTempPath(),
                    "Pegasus.WebCompositionTests",
                    Guid.NewGuid().ToString("N"))
            }
            : new Dictionary<string, string?>();
        using var factory = new ConfiguredWebApplicationFactory(environment, settings);
        using var scope = factory.Services.CreateScope();

        Assert.IsType(
            expectedArtifactStoreType,
            scope.ServiceProvider.GetRequiredService<IIntakeArtifactStore>());
        Assert.IsType<GetOperationsSnapshot>(
            scope.ServiceProvider.GetRequiredService<IGetOperationsSnapshot>());
    }

    [Fact]
    public void ProductionHostComposesBoxCustodyAndTheStaffDocumentSurface()
    {
        // ProductionCompositionTests wire AddPegasusInfrastructure directly, so
        // only a real-host resolution proves Program.cs still passes the
        // production storage profile through to composition.
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>());
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.IsType<BoxCaseCustody>(services.GetRequiredService<ICaseCustody>());
        Assert.IsType<BoxDocumentContentStore>(
            services.GetRequiredService<IDocumentContentStore>());
        Assert.NotNull(services.GetRequiredService<IAddCaseDocument>());
        Assert.NotNull(services.GetRequiredService<IDownloadCaseDocument>());
        Assert.NotNull(services.GetRequiredService<IExportCaseDocuments>());
        Assert.NotNull(services.GetRequiredService<IExportCaseBundle>());
    }
}

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
    internal const string TestBoxConfigJson = """
    {
      "boxAppSettings": {
        "clientID": "test-client-id",
        "appAuth": {
          "publicKeyID": "test-key-id",
          "privateKey": "test-private-key",
          "passphrase": "test-passphrase"
        }
      },
      "enterpriseID": "test-enterprise-id"
    }
    """;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var effectiveSettings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Runtime:Profile"] = environment.Equals("Production", StringComparison.Ordinal)
                ? "Production"
                : "DevelopmentOffline",
            ["ConnectionStrings:Pegasus"] =
                "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_ConfiguredFactory;" +
                "Integrated Security=true;Encrypt=false",
            ["AzureIdentity:WebClientId"] = "10213243-5465-7687-98a9-bacbdcedfe0f",
            ["TransportStorage:AccountName"] = "pegasustransporttest",
            ["IntakeQueue:ServiceUri"] = "https://pegasustransporttest.queue.core.windows.net/",
            ["CustodyStorage:AccountName"] = "pegasuscustodytest",
            ["CustodyStorage:ServiceUri"] = "https://pegasuscustodytest.blob.core.windows.net/",
            // The Production profile composes Box-backed custody, so a host needs
            // Box settings to start. These are inert test credentials; no Box call
            // is made by composing them.
            ["Box:BaseUri"] = "https://api.box.com/2.0/",
            ["Box:UploadUri"] = "https://upload.box.com/api/2.0/",
            ["Box:RootFolderId"] = "405543781910",
            ["Box:HoldingFolderId"] = "test-holding-folder",
            ["Box:ConfigJson"] = TestBoxConfigJson,
            ["Box:ClientSecret"] = "test-client-secret",
            ["Graph:BaseUri"] = "https://graph.microsoft.com/v1.0/",
            ["Graph:TenantId"] = "858cf5b3-aa0a-47a6-9b40-4851fd0afa94",
            ["Graph:ChangeNotificationClientState"] = "integration-client-state",
            // EXT-04: Production composes the EVA API submission route, so a
            // host needs EVA settings to start. These are inert test
            // credentials; no EVA call is made by composing them.
            ["Eva:BaseUri"] = "https://sentry.evasoftware.co.uk/api/",
            ["Eva:ClientId"] = "test-eva-client",
            ["Eva:ClientSecret"] = "test-eva-secret",
            ["Eva:RequestFrom"] = "COLLENGAPI",
            ["Eva:InspectionType"] = "Vehicle Damage Inspection",
            ["Eva:InstructionEmail"] = "digital@collisionengineers.co.uk"
        };
        foreach (var setting in settings)
        {
            effectiveSettings[setting.Key] = setting.Value;
        }

        builder.UseEnvironment(environment);
        foreach (var setting in effectiveSettings.Where(setting => setting.Value is not null))
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(effectiveSettings));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PEGASUS_TEST_UI_CAPTURE_DIR")))
            {
                services.AddTransient<IStartupFilter, TestUiResponseCaptureStartupFilter>();
            }
        });
    }
}
