using System.Globalization;
using System.Text.Json;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

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
                "20260729150000_DocumentCustodyAndRequests",
                "20260729152105_WorkflowTriageEmailEvidence",
                "20260729160000_CaseWorkflowRuntime",
                "20260729170000_MailboxRouteAudit",
                "20260729171000_CaseAcceptanceReplay",
                "20260729172000_CaseHoldState",
                "20260729173000_AuditIdentityEvidence",
                "20260729174000_MailboxPoisonRecovery",
                "20260729175000_CaseEvidenceAndReplacement",
                "20260729176000_AzureSqlRuntimeLeastPrivilege",
                "20260729180000_AdministrationPolicies",
                "20260729181000_VehicleWorkflow",
                "20260729182000_EvaHandoffPersistence",
                "20260729183000_SentEvidencePolling",
                "20260729184000_DueChaserSweep",
                "20260729185000_TypedCaseDataCompleteness",
                "20260729186000_CaseTasksArchive",
                "20260729187000_OrganizationPrincipalAdministration",
                "20260729188000_IntakeResolutionAndAssociations",
                "20260729189000_IdentityBootstrapAndOAuthAdministration",
                "20260729190000_CaseEditLeaseReplay",
                "20260729191000_OperationsProjectionIndexes",
                "20260729192000_TriageReplaySnapshots",
                "20260729193000_UniqueTriageResponseEvidenceLink",
                "20260729199000_RuntimeRoleReconciliation",
                "20260730203141_ThirdPartyVehicleEvidenceAndRemoveBootstrap",
                "20260730203833_RemoveDormantOpenIddict",
                "20260801220500_GrantWebMigrationHistoryRead",
                "20260803014608_ProviderInspectionModeSetting",
                "20260803071539_ImageIntakeRegistration",
                "20260803151159_AutomationActorOpenIddict"
            ],
            (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictApplications'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictAuthorizations'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictScopes'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictTokens'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ImageIntakes'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ImageIntakeSequences'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ImageVrmSuggestions'"));
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
        Assert.Equal(16, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderDomainEvidence"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'Cases'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseSequences'"));
        Assert.Equal(6, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Cases')
              AND name IN (
                  N'IX_Cases_AuditReference',
                  N'IX_Cases_OriginIntakeReceiptId',
                  N'IX_Cases_PrincipalId',
                  N'IX_Cases_Reference',
                  N'IX_Cases_SequenceLineageId_Year_Sequence',
                  N'IX_Cases_StandaloneAuditEvidenceId')
            """));
        Assert.Equal(6, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Cases')
              AND name LIKE N'IX_Cases[_]%'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Cases')
              AND name = N'IX_Cases_AuditReference'
              AND is_unique = 1
              AND has_filter = 1
            """));
        Assert.Equal(3, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'CaseIntakeLinks')
              AND name IN (
                  N'ExpectedIntakeVersion',
                  N'AcceptanceCommandMaterialJson',
                  N'AcceptanceCommandFingerprint')
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseWorkflows'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseWorkflowEvents'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseDueWork'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseManualChases'"));
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
        Assert.Equal(1, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("IntakeReceiptEvents"));
        Assert.Equal(0, await database.CountAsync("Cases"));
        Assert.Equal(0, await database.CountAsync("CaseSequences"));
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

/// <summary>
/// Where a disposable test database's schema came from.
/// </summary>
internal enum LocalDbSchemaOrigin
{
    /// <summary>No migration was applied.</summary>
    Empty,

    /// <summary>The migration stream was applied to this database.</summary>
    Migrated,

    /// <summary>The once-per-run migrated template was restored.</summary>
    Template
}

internal sealed class LocalDbTestDatabase : IAsyncDisposable
{
    internal const string Prefix = "Pegasus_Test_";

    /// <summary>
    /// How long a database-lifecycle statement may take.
    /// </summary>
    /// <remarks>
    /// CREATE, RESTORE, BACKUP and DROP all queue behind one another on the
    /// instance, so with test classes running in parallel the 30-second
    /// default turns load into a failure that looks like a timeout. One
    /// number, because they contend for the same thing.
    /// </remarks>
    internal const int LifecycleCommandTimeoutSeconds = 300;

    /// <summary>
    /// Overrides the data source for the SQL Server instance these tests use.
    /// </summary>
    /// <remarks>
    /// SQL Server Express LocalDB exists only on Windows. Leaving this unset
    /// keeps the Windows default exactly as it was; setting it lets a Linux
    /// workstation point the same tests at a SQL Server container. The engine,
    /// migration stream and assertions are identical either way.
    /// </remarks>
    private const string DataSourceVariable = "PEGASUS_TEST_SQL_DATASOURCE";
    private const string UserVariable = "PEGASUS_TEST_SQL_USER";
    private const string PasswordVariable = "PEGASUS_TEST_SQL_PASSWORD";

    /// <summary>
    /// Whether <c>PEGASUS_TEST_SQL_DATASOURCE</c> points these tests at an
    /// external SQL Server instead of LocalDB.
    /// </summary>
    internal static bool UsesExternalDataSource =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataSourceVariable));

    private readonly ServiceProvider services;
    private bool disposed;

    private static string BuildConnectionString(string databaseName)
    {
        var dataSource = Environment.GetEnvironmentVariable(DataSourceVariable);
        var builder = new SqlConnectionStringBuilder
        {
            InitialCatalog = databaseName,
            ConnectTimeout = 15,
            MultipleActiveResultSets = true
        };

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            builder.DataSource = @"(localdb)\MSSQLLocalDB";
            builder.IntegratedSecurity = true;
            builder.Encrypt = false;
            return builder.ConnectionString;
        }

        builder.DataSource = dataSource;
        var user = Environment.GetEnvironmentVariable(UserVariable);
        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"{DataSourceVariable} is set, so {UserVariable} and {PasswordVariable} are also required.");
        }

        builder.UserID = user;
        builder.Password = password;
        builder.IntegratedSecurity = false;
        // The container presents a self-signed certificate.
        builder.Encrypt = true;
        builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }

    private LocalDbTestDatabase(
        string databaseName,
        Action<DbContextOptionsBuilder>? configureDatabase,
        Func<IServiceProvider, string>? localArtifactRootFactory,
        Action<IServiceCollection>? configureServices)
    {
        DatabaseName = databaseName;
        ConnectionString = BuildConnectionString(databaseName);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPegasusInfrastructure(
            (_, options) =>
            {
                options.UseSqlServer(ConnectionString);
                configureDatabase?.Invoke(options);
            },
            localArtifactRootFactory);
        configureServices?.Invoke(serviceCollection);
        services = serviceCollection.BuildServiceProvider(validateScopes: true);
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    /// <summary>
    /// How this database got its schema. A test that means to exercise the
    /// template must assert this, or a broken template is a slow pass.
    /// </summary>
    public LocalDbSchemaOrigin SchemaOrigin { get; private set; } = LocalDbSchemaOrigin.Empty;

    public SqlConnection CreateConnection() => new(ConnectionString);

    public AsyncServiceScope CreateAsyncScope() => services.CreateAsyncScope();

    public static async Task<LocalDbTestDatabase> CreateAsync(
        bool migrate = true,
        Action<DbContextOptionsBuilder>? configureDatabase = null,
        Func<IServiceProvider, string>? localArtifactRootFactory = null,
        Action<IServiceCollection>? configureServices = null,
        bool useTemplate = true)
    {
        var database = new LocalDbTestDatabase(
            Prefix + Guid.NewGuid().ToString("N"),
            configureDatabase,
            localArtifactRootFactory,
            configureServices);
        try
        {
            // An unmigrated database is what several tests are about, so the
            // template is only ever a substitute for migrating.
            var template = migrate && useTemplate
                ? await LocalDbTemplateDatabase.GetAsync()
                : null;
            if (template is not null)
            {
                await database.RestoreFromTemplateAsync(template);
                database.SchemaOrigin = LocalDbSchemaOrigin.Template;
                return database;
            }

            await database.CreateEmptyDatabaseAsync();
            if (migrate)
            {
                await database.MigrateAsync();
                database.SchemaOrigin = LocalDbSchemaOrigin.Migrated;
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
            "IntakeReceipts" or "InstructionDrafts" or "IntakeReceiptEvents"
                or "Cases" or "CaseSequences" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        return ScalarAsync<int>($"SELECT COUNT(*) FROM [{allowed}]");
    }

    public async Task<T> ScalarAsync<T>(string commandText)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return value is T result
            ? result
            : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
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
            drop.CommandTimeout = LifecycleCommandTimeoutSeconds;
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
        command.CommandTimeout = LifecycleCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    private async Task RestoreFromTemplateAsync(LocalDbTemplateSnapshot template)
    {
        // The restore creates the database, so it carries the same name guard
        // the create path carries.
        ValidateExactDisposableName(DatabaseName);
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"RESTORE DATABASE [{DatabaseName}] FROM DISK = @backupPath WITH " +
            "MOVE @dataLogicalName TO @dataFile, MOVE @logLogicalName TO @logFile, RECOVERY";
        command.Parameters.AddWithValue("@backupPath", template.BackupPath);
        command.Parameters.AddWithValue("@dataLogicalName", template.DataLogicalName);
        command.Parameters.AddWithValue(
            "@dataFile",
            LocalDbTemplateDatabase.Combine(template.DataDirectory, DatabaseName + ".mdf"));
        command.Parameters.AddWithValue("@logLogicalName", template.LogLogicalName);
        command.Parameters.AddWithValue(
            "@logFile",
            LocalDbTemplateDatabase.Combine(template.DataDirectory, DatabaseName + "_log.ldf"));
        command.CommandTimeout = LifecycleCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    internal static string MasterConnectionString() => BuildConnectionString("master");

    /// <summary>
    /// The exact shape of a disposable test database's name.
    /// </summary>
    /// <remarks>
    /// One definition, because every create, restore, and drop is guarded by
    /// it. Anything failing this rule belongs to someone else and is never
    /// touched.
    /// </remarks>
    internal static bool IsDisposableName(string databaseName) =>
        databaseName.StartsWith(Prefix, StringComparison.Ordinal)
        && databaseName.Length == Prefix.Length + 32
        && Guid.TryParseExact(databaseName[Prefix.Length..], "N", out _);

    private static void ValidateExactDisposableName(string databaseName) =>
        Assert.True(
            IsDisposableName(databaseName),
            $"'{databaseName}' is not a disposable test database name.");
}

internal sealed record PersistedReceiptEvent(
    Guid IntakeReceiptId,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string DetailsJson);
