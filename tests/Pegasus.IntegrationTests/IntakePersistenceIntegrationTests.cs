using System.Globalization;
using System.Text.Json;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class IntakePersistenceIntegrationTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CommittedMigrationCreatesTheSqlServerSchema()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);

        await database.MigrateAsync();

        await using var context = await database.CreateContextAsync();
        Assert.Equal(
            [
                "20260724104624_InitialProviderNeutralIntake",
                "20260727170804_ProviderDomainReferenceSnapshotV1",
                "20260730101145_StaffTriageCaseWorkspaceV1",
            ],
            (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeReceipts'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeAssets'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'InstructionDrafts'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeReceiptEvents'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ProviderDomainPackages'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ProviderReferences'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ProviderDomainEvidence'"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderDomainPackages"));
        Assert.Equal(11, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderReferences"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'Cases'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Cases"));
    }

    [Fact]
    public async Task EightConcurrentDistinctSourceIdentitiesPersistEightPreCaseDrafts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var records = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            database.StoreAsync(CreateDraft(index, IntakeDecision.DraftReady))));

        Assert.Equal(8, records.Select(record => record.Id).Distinct().Count());
        Assert.All(records, record =>
        {
            Assert.NotNull(record.InstructionDraft);
        });
        Assert.Equal(8, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(8, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(8, await database.CountAsync("IntakeReceiptEvents"));
    }

    [Fact]
    public async Task EightConcurrentSameSourceIdentityCallsCreateOneReceiptAndDraft()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, IntakeDecision.DraftReady);

        var records = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => database.StoreAsync(draft)));

        Assert.Single(records.Select(record => record.Id).Distinct());
        Assert.All(records, record =>
        {
            Assert.NotNull(record.InstructionDraft);
        });
        Assert.Equal(1, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(1, await database.CountAsync("IntakeReceiptEvents"));
    }

    [Fact]
    public async Task FailedReceiptEventInsertRollsBackReceiptAndTypedDraftBeforeRetry()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.ExecuteAsync(
            "CREATE TRIGGER [FailReceiptEventInsert] ON [dbo].[IntakeReceiptEvents] INSTEAD OF INSERT AS " +
            "BEGIN THROW 51000, 'Deliberate integration-test receipt-event failure.', 1; END");
        var draft = CreateDraft(1, IntakeDecision.DraftReady);

        await Assert.ThrowsAsync<DbUpdateException>(() => database.StoreAsync(draft));

        Assert.Equal(0, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(0, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(0, await database.CountAsync("IntakeReceiptEvents"));
        await database.ExecuteAsync("DROP TRIGGER [dbo].[FailReceiptEventInsert]");

        var retried = await database.StoreAsync(draft);

        Assert.NotNull(retried.InstructionDraft);
        Assert.Equal(1, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(1, await database.CountAsync("IntakeReceiptEvents"));
    }

    [Fact]
    public async Task ConfirmedDraftCannotCreateCaseOrReferenceCounterPersistence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var record = await database.StoreAsync(CreateDraft(1, IntakeDecision.DraftReady));

        Assert.NotNull(record.InstructionDraft);
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'Cases'"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM Cases"));
        Assert.Equal(0, await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseSequences"));
    }

    [Fact]
    public async Task DraftReceiptPersistsReceiptHistoryContents()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, IntakeDecision.DraftReady);

        var record = await database.StoreAsync(draft);
        var receiptEvent = await database.ReadSingleReceiptEventAsync();

        Assert.Equal(record.Id, receiptEvent.IntakeReceiptId);
        Assert.Equal("intake_receipt_recorded", receiptEvent.EventType);
        Assert.Equal("LocalDB integration test", receiptEvent.Actor);
        Assert.Equal(FixedTime, receiptEvent.OccurredAtUtc);
        using var details = JsonDocument.Parse(receiptEvent.DetailsJson);
        Assert.Equal(1, details.RootElement.GetProperty("version").GetInt32());
        var data = details.RootElement.GetProperty("data");
        Assert.Equal("draft_ready", data.GetProperty("decision").GetString());
        Assert.Equal("manual_upload", data.GetProperty("sourceChannel").GetString());
        Assert.Equal(draft.SourceIdentity.ExternalReceiptToken,
            data.GetProperty("externalReceiptToken").GetString());
        Assert.False(data.TryGetProperty("caseReference", out _));
        Assert.False(data.TryGetProperty("caseCreationAuthorized", out _));
        Assert.Equal(draft.SourceHash, data.GetProperty("sourceHash").GetString());
    }

    [Fact]
    public async Task NeedsSortingFilterReturnsOnlyLiteralNeedsSortingReceipts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.StoreAsync(CreateDraft(1, IntakeDecision.NeedsSorting));
        await database.StoreAsync(CreateDraft(2, IntakeDecision.OcrRequired));
        await database.StoreAsync(CreateDraft(3, IntakeDecision.Unsupported));
        await database.StoreAsync(CreateDraft(4, IntakeDecision.TechnicalFailure));

        var result = await database.ListAsync(IntakeDecision.NeedsSorting);

        var receipt = Assert.Single(result);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Equal("source-1.bin", receipt.SourceFileName);
        Assert.Equal(new IntakeQueueCounts(0, 1), await database.GetCountsAsync());
    }

    private static IntakeReceiptDraft CreateDraft(
        int id,
        IntakeDecision decision) => new(
        $"source-{id}.bin",
        "application/octet-stream",
        id,
        id.ToString("X64", CultureInfo.InvariantCulture),
        new(IntakeSourceChannel.ManualUpload, id.ToString("x32", CultureInfo.InvariantCulture)),
        FixedTime,
        FixedTime,
        "LocalDB integration test",
        decision,
        $"{decision} integration-test decision",
        [new(IntakeEvidenceSource.SystemDefault, IntakeEvidenceStrength.Weak, IntakeEvidenceFinding.Information,
            "integration-test", "Persistence boundary evidence")],
        [new("Instruction date", "2031-05-06", [], true, false)],
        decision == IntakeDecision.DraftReady
            ? new("QDOS", null, null, null, null, null, null, null, null, new DateOnly(2031, 5, 6), null)
            : null,
        [],
        null,
        null,
        "controlled_test_reader",
        "1",
        decision == IntakeDecision.DraftReady ? QdosInstructionExtractionPolicy.Key : null,
        decision == IntakeDecision.DraftReady ? QdosInstructionExtractionPolicy.Version : null);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalDbFixtureDefinition
{
    public const string Name = "Disposable LocalDB";
}

internal sealed class LocalDbTestDatabase : IAsyncDisposable
{
    private const string Prefix = "Pegasus_Test_";
    private readonly ServiceProvider services;
    private bool disposed;

    private LocalDbTestDatabase(string databaseName)
    {
        DatabaseName = databaseName;
        ConnectionString = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            InitialCatalog = databaseName,
            IntegratedSecurity = true,
            Encrypt = false,
            ConnectTimeout = 15,
            MultipleActiveResultSets = true
        }.ConnectionString;

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPegasusInfrastructure((_, options) => options.UseSqlServer(ConnectionString));
        services = serviceCollection.BuildServiceProvider(validateScopes: true);
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public static async Task<LocalDbTestDatabase> CreateAsync(bool migrate = true)
    {
        var database = new LocalDbTestDatabase(Prefix + Guid.NewGuid().ToString("N"));
        try
        {
            await database.CreateEmptyDatabaseAsync();
            if (migrate)
            {
                await database.MigrateAsync();
            }

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async Task MigrateAsync()
    {
        await using var context = await CreateContextAsync();
        await context.Database.MigrateAsync();
    }

    public async Task<PegasusDbContext> CreateContextAsync()
    {
        var factory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        return await factory.CreateDbContextAsync();
    }

    public async Task<IntakeReceipt> StoreAsync(IntakeReceiptDraft draft)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>()
            .StoreAsync(draft, CancellationToken.None);
    }

    public async Task<IReadOnlyList<IntakeReceiptSummary>> ListAsync(IntakeDecision decision)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .ListAsync(decision, CancellationToken.None);
    }

    public async Task<IntakeQueueCounts> GetCountsAsync()
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetCountsAsync(CancellationToken.None);
    }

    public Task<int> CountAsync(string tableName)
    {
        var allowed = tableName switch
        {
            "IntakeReceipts" or "InstructionDrafts" or "IntakeReceiptEvents" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        return ScalarAsync<int>($"SELECT COUNT(*) FROM [{allowed}]");
    }

    public async Task<T> ScalarAsync<T>(string commandText)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public async Task ExecuteAsync(string commandText)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<PersistedReceiptEvent> ReadSingleReceiptEventAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT IntakeReceiptId, EventType, Actor, OccurredAtUtc, DetailsJson FROM IntakeReceiptEvents";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var result = new PersistedReceiptEvent(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetString(4));
        Assert.False(await reader.ReadAsync());
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await services.DisposeAsync();
        ValidateExactDisposableName(DatabaseName);

        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();
        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText =
                $"IF DB_ID(@databaseName) IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{DatabaseName}]; END";
            drop.Parameters.AddWithValue("@databaseName", DatabaseName);
            await drop.ExecuteNonQueryAsync();
        }

        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT DB_ID(@databaseName)";
        verify.Parameters.AddWithValue("@databaseName", DatabaseName);
        Assert.Equal(DBNull.Value, await verify.ExecuteScalarAsync());
    }

    private async Task CreateEmptyDatabaseAsync()
    {
        ValidateExactDisposableName(DatabaseName);
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{DatabaseName}]";
        await command.ExecuteNonQueryAsync();
    }

    private static string MasterConnectionString() => new SqlConnectionStringBuilder
    {
        DataSource = @"(localdb)\MSSQLLocalDB",
        InitialCatalog = "master",
        IntegratedSecurity = true,
        Encrypt = false,
        ConnectTimeout = 15
    }.ConnectionString;

    private static void ValidateExactDisposableName(string databaseName)
    {
        Assert.StartsWith(Prefix, databaseName, StringComparison.Ordinal);
        Assert.Equal(Prefix.Length + 32, databaseName.Length);
        Assert.True(Guid.TryParseExact(databaseName[Prefix.Length..], "N", out _));
    }
}

internal sealed record PersistedReceiptEvent(
    Guid IntakeReceiptId,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string DetailsJson);
