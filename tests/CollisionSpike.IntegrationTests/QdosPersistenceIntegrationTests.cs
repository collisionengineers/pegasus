using System.Globalization;
using System.Text.Json;
using CollisionSpike.Core.Intake.Qdos;
using CollisionSpike.Infrastructure;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class QdosPersistenceIntegrationTests
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
                "20260723075441_InitialQdosIntake",
                "20260723125212_AddIntakeAssets",
                "20260723170000_AddTypedQdosDraftAndSourceIdentity",
                "20260723171000_RemoveRetiredQdosCaseAllocation"
            ],
            (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'QdosIntakeReceipts'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'QdosIntakeAssets'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'QdosTypedDrafts'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'AuditEvents'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'Cases', N'PrincipalYearCounters')"));
    }

    [Fact]
    public async Task PopulatedAssetSchemaUpgradePreservesRetiredAllocationEvidenceAndBackfillsNullTypedDraft()
    {
        const string legacyEvidence = "[{\"signal\":\"legacy-evidence\"}]";
        const string legacyFields = "[{\"name\":\"Claim number\",\"suggestedValue\":\"LEGACY-001\"}]";
        const string legacyAuditDetails = "{\"legacy\":\"history\"}";
        var receiptId = new Guid("10000000-0000-0000-0000-000000000001");
        var caseId = new Guid("20000000-0000-0000-0000-000000000002");
        var auditId = new Guid("30000000-0000-0000-0000-000000000003");
        var assetId = new Guid("40000000-0000-0000-0000-000000000004");
        var sourceHash = new string('A', 64);
        var assetHash = new string('B', 64);
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await database.MigrateToAsync("20260723125212_AddIntakeAssets");
        await database.ExecuteAsync($"""
            INSERT INTO [Cases] ([Id], [PrincipalCode], [CaseReference], [CreatedAtUtc])
            VALUES ('{caseId:D}', N'QDOS', N'QDOS31042', '2031-05-06T10:30:00+00:00');
            INSERT INTO [PrincipalYearCounters] ([PrincipalCode], [Year], [CurrentSequence])
            VALUES (N'QDOS', 2031, 42);
            INSERT INTO [QdosIntakeReceipts]
                ([Id], [SourceFileName], [MediaType], [SourceLength], [SourceHash], [ReceivedAtUtc],
                 [Decision], [DecisionReason], [EvidenceJson], [FieldsJson], [FailureCode], [FailureReason],
                 [CaseId], [OcrCandidatesJson])
            VALUES
                ('{receiptId:D}', N'legacy-source.eml', N'message/rfc822', 321, N'{sourceHash}',
                 '2031-05-06T10:30:00+00:00', N'ConfirmedQdos', N'Legacy confirmed receipt',
                 N'{legacyEvidence}', N'{legacyFields}', NULL, NULL, '{caseId:D}', N'[]');
            INSERT INTO [QdosIntakeAssets]
                ([Id], [IntakeReceiptId], [SourceLabel], [FileName], [MediaType], [Kind], [Disposition],
                 [ContentLength], [ContentHash], [StorageKey], [PageNumber], [BoundsJson], [WidthPixels], [HeightPixels])
            VALUES
                ('{assetId:D}', '{receiptId:D}', N'legacy source', N'legacy-source.eml', N'message/rfc822',
                 N'Source', N'Source', 321, N'{assetHash}', N'sha256/BB/{assetHash}', NULL, NULL, NULL, NULL);
            INSERT INTO [AuditEvents]
                ([Id], [IntakeReceiptId], [CaseId], [EventType], [Actor], [OccurredAtUtc], [DetailsJson])
            VALUES
                ('{auditId:D}', '{receiptId:D}', '{caseId:D}', N'LegacyReceiptRecorded', N'legacy-test',
                 '2031-05-06T10:30:00+00:00', N'{legacyAuditDetails}');
            """);

        await database.MigrateAsync();

        Assert.Equal(4, await database.ScalarAsync<int>("SELECT COUNT(*) FROM [__EFMigrationsHistory]"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [QdosTypedDrafts] WHERE [IntakeReceiptId] = '{receiptId:D}' " +
            "AND [PrincipalCode] = N'QDOS' AND [ClaimantName] IS NULL AND [ClaimNumber] IS NULL " +
            "AND [VehicleRegistration] IS NULL AND [VehicleMake] IS NULL AND [VehicleModel] IS NULL " +
            "AND [VehicleMileage] IS NULL AND [AccidentCircumstances] IS NULL AND [DateOfIncident] IS NULL " +
            "AND [InstructionDate] IS NULL AND [InspectionAddress] IS NULL"));
        Assert.Equal(sourceHash, await database.ScalarAsync<string>(
            $"SELECT [SourceHash] FROM [QdosIntakeReceipts] WHERE [Id] = '{receiptId:D}'"));
        Assert.Equal("DraftReady", await database.ScalarAsync<string>(
            $"SELECT [Decision] FROM [QdosIntakeReceipts] WHERE [Id] = '{receiptId:D}'"));
        Assert.Equal(legacyEvidence, await database.ScalarAsync<string>(
            $"SELECT [EvidenceJson] FROM [QdosIntakeReceipts] WHERE [Id] = '{receiptId:D}'"));
        Assert.Equal(legacyFields, await database.ScalarAsync<string>(
            $"SELECT [FieldsJson] FROM [QdosIntakeReceipts] WHERE [Id] = '{receiptId:D}'"));
        Assert.Equal("ManualUpload", await database.ScalarAsync<string>(
            $"SELECT [SourceChannel] FROM [QdosIntakeReceipts] WHERE [Id] = '{receiptId:D}'"));
        Assert.Equal(32, (await database.ScalarAsync<string>(
            $"SELECT [ExternalReceiptToken] FROM [QdosIntakeReceipts] WHERE [Id] = '{receiptId:D}'")).Length);
        Assert.Equal(assetHash, await database.ScalarAsync<string>(
            $"SELECT [ContentHash] FROM [QdosIntakeAssets] WHERE [Id] = '{assetId:D}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM [AuditEvents] WHERE [EventType] = N'LegacyReceiptRecorded' " +
            "AND [DetailsJson] = N'{\"legacy\":\"history\"}'"));

        var recordedJson = await database.ScalarAsync<string>(
            "SELECT [DetailsJson] FROM [AuditEvents] " +
            "WHERE [EventType] = N'RetiredDevelopmentAllocationRecorded'");
        using var recorded = JsonDocument.Parse(recordedJson);
        Assert.True(recorded.RootElement.GetProperty("retiredDevelopmentTestProof").GetBoolean());
        Assert.Equal("QDOS", recorded.RootElement.GetProperty("principalCode").GetString());
        Assert.Equal("QDOS31042", recorded.RootElement.GetProperty("caseReference").GetString());
        Assert.Equal(2031, recorded.RootElement.GetProperty("counterYear").GetInt32());
        Assert.Equal(42, recorded.RootElement.GetProperty("counterCurrentSequence").GetInt32());
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'Cases', N'PrincipalYearCounters')"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE name = N'CaseId' " +
            "AND object_id IN (OBJECT_ID(N'QdosIntakeReceipts'), OBJECT_ID(N'AuditEvents'))"));
    }

    [Fact]
    public async Task EightConcurrentDistinctSourceIdentitiesPersistEightPreCaseDrafts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var records = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            database.StoreAsync(CreateDraft(index, QdosIntakeDecision.DraftReady))));

        Assert.Equal(8, records.Select(record => record.Id).Distinct().Count());
        Assert.All(records, record =>
        {
            Assert.NotNull(record.TypedDraft);
        });
        Assert.Equal(8, await database.CountAsync("QdosIntakeReceipts"));
        Assert.Equal(8, await database.CountAsync("QdosTypedDrafts"));
        Assert.Equal(8, await database.CountAsync("AuditEvents"));
    }

    [Fact]
    public async Task EightConcurrentSameSourceIdentityCallsCreateOneReceiptAndDraft()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, QdosIntakeDecision.DraftReady);

        var records = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => database.StoreAsync(draft)));

        Assert.Single(records.Select(record => record.Id).Distinct());
        Assert.All(records, record =>
        {
            Assert.NotNull(record.TypedDraft);
        });
        Assert.Equal(1, await database.CountAsync("QdosIntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("QdosTypedDrafts"));
        Assert.Equal(1, await database.CountAsync("AuditEvents"));
    }

    [Fact]
    public async Task FailedAuditInsertRollsBackReceiptAndTypedDraftBeforeRetry()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.ExecuteAsync(
            "CREATE TRIGGER [FailAuditInsert] ON [dbo].[AuditEvents] INSTEAD OF INSERT AS " +
            "BEGIN THROW 51000, 'Deliberate integration-test audit failure.', 1; END");
        var draft = CreateDraft(1, QdosIntakeDecision.DraftReady);

        await Assert.ThrowsAsync<DbUpdateException>(() => database.StoreAsync(draft));

        Assert.Equal(0, await database.CountAsync("QdosIntakeReceipts"));
        Assert.Equal(0, await database.CountAsync("QdosTypedDrafts"));
        Assert.Equal(0, await database.CountAsync("AuditEvents"));
        await database.ExecuteAsync("DROP TRIGGER [dbo].[FailAuditInsert]");

        var retried = await database.StoreAsync(draft);

        Assert.NotNull(retried.TypedDraft);
        Assert.Equal(1, await database.CountAsync("QdosIntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("QdosTypedDrafts"));
        Assert.Equal(1, await database.CountAsync("AuditEvents"));
    }

    [Fact]
    public async Task ConfirmedDraftCannotCreateCaseOrReferenceCounterPersistence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var record = await database.StoreAsync(CreateDraft(1, QdosIntakeDecision.DraftReady));

        Assert.NotNull(record.TypedDraft);
        Assert.Equal(1, await database.CountAsync("QdosIntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("AuditEvents"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'Cases', N'PrincipalYearCounters')"));
    }

    [Fact]
    public async Task ConfirmedReceiptPersistsCompleteBusinessAuditContents()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, QdosIntakeDecision.DraftReady);

        var record = await database.StoreAsync(draft);
        var audit = await database.ReadSingleAuditAsync();

        Assert.Equal(record.Id, audit.IntakeReceiptId);
        Assert.Equal("QdosIntakeReceived", audit.EventType);
        Assert.Equal("LocalDB integration test", audit.Actor);
        Assert.Equal(FixedTime, audit.OccurredAtUtc);
        using var details = JsonDocument.Parse(audit.DetailsJson);
        Assert.Equal("DraftReady", details.RootElement.GetProperty("decision").GetString());
        Assert.Equal("ManualUpload", details.RootElement.GetProperty("sourceChannel").GetString());
        Assert.Equal(draft.SourceIdentity.ExternalReceiptToken,
            details.RootElement.GetProperty("externalReceiptToken").GetString());
        Assert.False(details.RootElement.TryGetProperty("caseReference", out _));
        Assert.False(details.RootElement.TryGetProperty("caseCreationAuthorized", out _));
        Assert.Equal(draft.SourceHash, details.RootElement.GetProperty("sourceHash").GetString());
    }

    [Fact]
    public async Task NeedsSortingFilterReturnsOnlyLiteralNeedsSortingReceipts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.StoreAsync(CreateDraft(1, QdosIntakeDecision.NeedsSorting));
        await database.StoreAsync(CreateDraft(2, QdosIntakeDecision.OcrRequired));
        await database.StoreAsync(CreateDraft(3, QdosIntakeDecision.Unsupported));
        await database.StoreAsync(CreateDraft(4, QdosIntakeDecision.TechnicalFailure));

        var result = await database.ListAsync(QdosIntakeDecision.NeedsSorting);

        var receipt = Assert.Single(result);
        Assert.Equal(QdosIntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Equal("source-1.bin", receipt.SourceFileName);
        Assert.Equal(new QdosQueueCounts(0, 1), await database.GetCountsAsync());
    }

    private static QdosIntakeDraft CreateDraft(
        int id,
        QdosIntakeDecision decision) => new(
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
        [new(QdosEvidenceSource.SystemDefault, QdosEvidenceStrength.Weak, QdosEvidenceFinding.Information,
            "integration-test", "Persistence boundary evidence")],
        [new("Instruction date", "2031-05-06", [], true, false)],
        decision == QdosIntakeDecision.DraftReady
            ? new("QDOS", null, null, null, null, null, null, null, null, new DateOnly(2031, 5, 6), null)
            : null,
        [],
        null,
        null);
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalDbFixtureDefinition
{
    public const string Name = "Disposable LocalDB";
}

internal sealed class LocalDbTestDatabase : IAsyncDisposable
{
    private const string Prefix = "CollisionSpikeV2_Test_";
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
        serviceCollection.AddCollisionSpikeInfrastructure((_, options) => options.UseSqlServer(ConnectionString));
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

    public async Task MigrateToAsync(string targetMigration)
    {
        await using var context = await CreateContextAsync();
        await context.GetService<IMigrator>().MigrateAsync(targetMigration);
    }

    public async Task<CollisionSpikeDbContext> CreateContextAsync()
    {
        var factory = services.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        return await factory.CreateDbContextAsync();
    }

    public async Task<QdosIntakeRecord> StoreAsync(QdosIntakeDraft draft)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQdosIntakeStore>()
            .StoreAsync(draft, CancellationToken.None);
    }

    public async Task<IReadOnlyList<QdosIntakeSummary>> ListAsync(QdosIntakeDecision decision)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>()
            .ListAsync(decision, CancellationToken.None);
    }

    public async Task<QdosQueueCounts> GetCountsAsync()
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>()
            .GetCountsAsync(CancellationToken.None);
    }

    public Task<int> CountAsync(string tableName)
    {
        var allowed = tableName switch
        {
            "QdosIntakeReceipts" or "QdosTypedDrafts" or "AuditEvents" => tableName,
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

    public async Task<PersistedAudit> ReadSingleAuditAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT IntakeReceiptId, EventType, Actor, OccurredAtUtc, DetailsJson FROM AuditEvents";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var result = new PersistedAudit(
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

internal sealed record PersistedAudit(
    Guid IntakeReceiptId,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string DetailsJson);
