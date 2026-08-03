using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Routing;
using Pegasus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class LocalIntakeAccessTests
{
    public static TheoryData<string, bool?> DeniedConfigurations => new()
    {
        { "Development", null },
        { "Development", false }
    };

    [Fact]
    public async Task RetiredQdosRouteAlwaysReturnsNotFoundWithoutPersistence()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync("/Intake/Qdos");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<Pegasus.Core.Intake.IIntakeReceiptQueries>();
        Assert.Empty(await queries.ListAsync(null, CancellationToken.None));
    }

    [Theory]
    [MemberData(nameof(DeniedConfigurations))]
    public async Task DisabledLocalIntakeReturnsNotFoundWithoutPersistence(
        string environment,
        bool? featureEnabled)
    {
        using var factory = new IntakeWebApplicationFactory(environment, featureEnabled);
        using var client = IntakeWebDriver.CreateClient(factory);

        foreach (var path in new[]
                 {
                      "/Intake",
                      $"/Intake/{Guid.NewGuid()}",
                     $"/Intake/{Guid.NewGuid()}/Source"
                 })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        using var uploadMultipart = new MultipartFormDataContent();
        using var uploadPost = await client.PostAsync("/Intake?handler=ReceiveIntake", uploadMultipart);
        Assert.Equal(HttpStatusCode.NotFound, uploadPost.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            Assert.Equal(0, await CountRowsIfPresentAsync(connection, DatabaseTable.IntakeReceipts));
            Assert.Equal(0, await CountRowsIfPresentAsync(connection, DatabaseTable.PrincipalYearCounters));
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Theory]
    [InlineData(
        "DevelopmentOffline",
        false,
        "The DevelopmentOffline runtime profile is permitted only in the Development environment.")]
    [InlineData(
        "Production",
        true,
        "Features:LocalIntake requires the DevelopmentOffline runtime profile.")]
    public void ProductionRejectsDevelopmentOnlyConfigurationBeforeAuthenticationStartup(
        string runtimeProfile,
        bool localIntakeEnabled,
        string expectedMessage)
    {
        using var factory = new ConfiguredWebApplicationFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["Runtime:Profile"] = runtimeProfile,
                ["ConnectionStrings:Pegasus"] =
                    $"Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_InvalidRuntimeProfile_{Guid.NewGuid():N};" +
                    "Integrated Security=true;Encrypt=false",
                ["Features:LocalIntake"] = localIntakeEnabled.ToString()
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var configurationException = Assert.IsType<InvalidOperationException>(exception.GetBaseException());

        Assert.Equal(expectedMessage, configurationException.Message);
    }

    [Theory]
    [InlineData("Box:BaseUri")]
    [InlineData("Box:UploadUri")]
    [InlineData("Box:RootFolderId")]
    [InlineData("Box:ConfigJson")]
    [InlineData("Box:ClientSecret")]
    public void ProductionFailsClosedWithoutTheBoxCustodyConfigurationItComposes(string missingKey)
    {
        // Production now composes Box-backed custody and managed document content,
        // so a missing Box setting must stop startup rather than silently leaving
        // the staff document surface unavailable.
        var configuration = new Dictionary<string, string?>
        {
            ["Runtime:Profile"] = "Production",
            ["ConnectionStrings:Pegasus"] =
                $"Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_MissingBox_{Guid.NewGuid():N};" +
                "Integrated Security=true;Encrypt=false",
            ["AzureIdentity:WebClientId"] = Guid.NewGuid().ToString("D"),
            ["TransportStorage:AccountName"] = "pegasustransport",
            ["CustodyStorage:AccountName"] = "pegasuscustody",
            ["CustodyStorage:ServiceUri"] = "https://pegasuscustody.blob.core.windows.net/",
            ["Box:BaseUri"] = "https://api.box.com/2.0/",
            ["Box:UploadUri"] = "https://upload.box.com/api/2.0/",
            ["Box:RootFolderId"] = "405543781910",
            ["Box:ConfigJson"] = "{}",
            ["Box:ClientSecret"] = "client-secret"
        };
        configuration[missingKey] = null;

        using var factory = new ConfiguredWebApplicationFactory("Production", configuration);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var configurationException = Assert.IsType<InvalidOperationException>(exception.GetBaseException());

        Assert.Equal(
            $"{missingKey} is required for the Production runtime profile.",
            configurationException.Message);
    }

    private static async Task<long> CountRowsIfPresentAsync(DbConnection connection, DatabaseTable table)
    {
        await using var existenceCommand = connection.CreateCommand();
        existenceCommand.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = @name";
        var parameter = existenceCommand.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = table switch
        {
            DatabaseTable.IntakeReceipts => "IntakeReceipts",
            DatabaseTable.PrincipalYearCounters => "PrincipalYearCounters",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        existenceCommand.Parameters.Add(parameter);
        if (Convert.ToInt64(await existenceCommand.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            return 0;
        }

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = table switch
        {
            DatabaseTable.IntakeReceipts => "SELECT COUNT(*) FROM IntakeReceipts",
            DatabaseTable.PrincipalYearCounters => "SELECT COUNT(*) FROM PrincipalYearCounters",
            _ => throw new ArgumentOutOfRangeException(nameof(table))
        };
        return Convert.ToInt64(await countCommand.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private enum DatabaseTable
    {
        IntakeReceipts,
        PrincipalYearCounters
    }
}
