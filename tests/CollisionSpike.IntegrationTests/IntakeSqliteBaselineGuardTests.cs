using CollisionSpike.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

public sealed class IntakeSqliteBaselineGuardTests
{
    [Fact]
    public async Task FreshDevelopmentDatabaseAppliesSingleProviderNeutralMigration()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Intake/Upload");
        response.EnsureSuccessStatusCode();

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        Assert.Equal(
            "20260724104624_InitialProviderNeutralIntake",
            await ScalarAsync<string>(connection, "SELECT MigrationId FROM __EFMigrationsHistory"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='IntakeReceipts'"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='InstructionDrafts'"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='IntakeAssets'"));
        Assert.Equal(1L, await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='IntakeAuditEvents'"));
    }

    [Fact]
    public async Task ExactCurrentBaselineIsAcceptedWithoutMutation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync("/Intake/Upload");
        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var before = await context.Database.GetAppliedMigrationsAsync();

        await DevelopmentSqliteBaselineGuard.ValidateAsync(context);

        Assert.Equal(before, await context.Database.GetAppliedMigrationsAsync());
    }

    [Theory]
    [InlineData("old_history")]
    [InlineData("schema_without_history")]
    [InlineData("random_table")]
    [InlineData("extra_column")]
    [InlineData("extra_index")]
    [InlineData("missing_column")]
    [InlineData("missing_index")]
    [InlineData("changed_foreign_key")]
    public async Task RefusedBaselineLeavesHistorySchemaAndSentinelUnchanged(string corruption)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        using var response = await client.GetAsync("/Intake/Upload");
        response.EnsureSuccessStatusCode();

        await using var connection = new SqliteConnection($"Data Source={factory.DatabasePath}");
        await connection.OpenAsync();
        await ExecuteAsync(connection, "PRAGMA user_version=4242;");
        await ExecuteAsync(connection,
            """
            INSERT INTO IntakeReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
                 ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
                 SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Decision,
                 DecisionReason, EvidenceJson, FieldsJson, FailureCode, FailureReason, OcrCandidatesJson)
            VALUES
                ('10000000-0000-0000-0000-000000000001', 'preserve-me.eml', 'message/rfc822', 1,
                 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA', 'manual_upload',
                 'preserve-me-token', '2031-05-06T10:30:00+00:00', '2031-05-06T10:30:00+00:00',
                 'controlled_test_reader', '1', NULL, NULL, 'needs_sorting', 'preserve sentinel',
                 '{"version":1,"data":[]}', '{"version":1,"data":[]}', NULL, NULL,
                 '{"version":1,"data":[]}');
            """);
        switch (corruption)
        {
            case "old_history":
                await ExecuteAsync(connection,
                    "UPDATE __EFMigrationsHistory SET MigrationId='20260723075441_InitialQdosIntake'");
                break;
            case "schema_without_history":
                await ExecuteAsync(connection, "DROP TABLE __EFMigrationsHistory");
                break;
            case "random_table":
                await ExecuteAsync(connection,
                    "CREATE TABLE BaselineSentinel (Value TEXT NOT NULL); INSERT INTO BaselineSentinel VALUES ('preserve-me');");
                break;
            case "extra_column":
                await ExecuteAsync(connection, "ALTER TABLE IntakeReceipts ADD COLUMN Unexpected TEXT NULL");
                break;
            case "extra_index":
                await ExecuteAsync(connection, "CREATE INDEX IX_Unexpected ON IntakeReceipts(MediaType)");
                break;
            case "missing_column":
                await ExecuteAsync(connection, "ALTER TABLE IntakeReceipts DROP COLUMN FailureReason");
                break;
            case "missing_index":
                await ExecuteAsync(connection, "DROP INDEX IX_IntakeReceipts_SourceHash");
                break;
            case "changed_foreign_key":
                await ExecuteAsync(connection,
                    """
                    ALTER TABLE IntakeAuditEvents RENAME TO IntakeAuditEventsOriginal;
                    CREATE TABLE IntakeAuditEvents (
                        Id TEXT NOT NULL PRIMARY KEY,
                        IntakeReceiptId TEXT NOT NULL,
                        EventType TEXT NOT NULL,
                        Actor TEXT NOT NULL,
                        OccurredAtUtc TEXT NOT NULL,
                        DetailsJson TEXT NOT NULL,
                        FOREIGN KEY (IntakeReceiptId) REFERENCES IntakeReceipts(Id) ON DELETE CASCADE
                    );
                    DROP TABLE IntakeAuditEventsOriginal;
                    CREATE INDEX IX_IntakeAuditEvents_IntakeReceiptId ON IntakeAuditEvents(IntakeReceiptId);
                    """);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }

        var before = await SnapshotAsync(connection);
        await connection.CloseAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DevelopmentSqliteBaselineGuard.ValidateAsync(context));

        await connection.OpenAsync();
        Assert.Equal(before, await SnapshotAsync(connection));
        Assert.Equal(4242L, await ScalarAsync<long>(connection, "PRAGMA user_version"));
        Assert.Equal("preserve-me.eml", await ScalarAsync<string>(connection,
            "SELECT SourceFileName FROM IntakeReceipts WHERE ExternalReceiptToken='preserve-me-token'"));
        if (corruption == "random_table")
        {
            Assert.Equal("preserve-me", await ScalarAsync<string>(connection, "SELECT Value FROM BaselineSentinel"));
        }
    }

    private static async Task<string> SnapshotAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT type || ':' || name || ':' || COALESCE(sql, '') FROM sqlite_master " +
            "WHERE name NOT LIKE 'sqlite_%' ORDER BY type, name";
        await using var reader = await command.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        var schema = string.Join('\n', lines);
        var hasHistory = await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'") == 1;
        var history = hasHistory
            ? await ScalarAsync<string>(connection,
                "SELECT COALESCE(group_concat(MigrationId || ':' || ProductVersion, '|'), '') FROM __EFMigrationsHistory")
            : "<absent>";
        return schema + "\nHISTORY:" + history;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(
            await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a scalar value."),
            typeof(T),
            System.Globalization.CultureInfo.InvariantCulture);
    }
}
