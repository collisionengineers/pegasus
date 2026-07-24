using System.Data.Common;
using System.Net;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

public sealed class LocalIntakeAccessTests
{
    public static TheoryData<string, bool?> DeniedConfigurations => new()
    {
        { "Development", null },
        { "Development", false },
        { "Production", true }
    };

    [Fact]
    public async Task RetiredQdosRouteAlwaysReturnsNotFoundWithoutPersistence()
    {
        using var factory = new IntakeWebApplicationFactory("Development", true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync("/Intake/Qdos");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<CollisionSpike.Core.Intake.IIntakeReceiptQueries>();
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
                     "/Intake/Upload",
                     "/Intake/Queue",
                     $"/Intake/Review/{Guid.NewGuid()}"
                 })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var multipart = new MultipartFormDataContent();
        using var post = await client.PostAsync("/Intake/Upload", multipart);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
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

    private static async Task<long> CountRowsIfPresentAsync(DbConnection connection, DatabaseTable table)
    {
        await using var existenceCommand = connection.CreateCommand();
        existenceCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        var parameter = existenceCommand.CreateParameter();
        parameter.ParameterName = "$name";
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
