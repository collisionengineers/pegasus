using System.Globalization;
using System.Text.Json;
using CollisionSpike.Core.Intake.Qdos;
using CollisionSpike.Infrastructure;
using CollisionSpike.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace CollisionSpike.IntegrationTests;

public sealed class QdosSqliteMigrationAdoptionTests
{
    [Fact]
    public async Task PersistentAssetSchemaWithoutHistoryIsAdoptedAndUpgradedWithoutLosingEvidence()
    {
        var receiptId = new Guid("51000000-0000-0000-0000-000000000001");
        var caseId = new Guid("52000000-0000-0000-0000-000000000002");
        var assetId = new Guid("53000000-0000-0000-0000-000000000003");
        const string legacyEvidence =
            "[{\"source\":\"SystemDefault\",\"strength\":\"Weak\",\"finding\":\"Information\"," +
            "\"signal\":\"legacy-evidence\",\"detail\":\"Legacy evidence retained.\"}]";
        const string legacyFields =
            "[{\"name\":\"Claim number\",\"suggestedValue\":\"LEGACY-SQLITE-001\"," +
            "\"candidates\":[],\"isDefaulted\":false,\"hasConflict\":false}]";
        const string legacyAuditDetails = "{\"legacy\":\"history\"}";
        var sourceHash = new string('C', 64);
        var assetHash = new string('D', 64);
        using var factory = new QdosWebApplicationFactory();
        Directory.CreateDirectory(Path.GetDirectoryName(factory.DatabasePath)!);
        await CreateAssetSchemaWithoutHistoryAsync(
            factory.DatabasePath,
            receiptId,
            caseId,
            assetId,
            sourceHash,
            assetHash,
            legacyEvidence,
            legacyFields,
            legacyAuditDetails);

        using var client = QdosWebDriver.CreateClient(factory);
        using var queue = await client.GetAsync("/Intake/Queue");
        queue.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IQdosIntakeQueries>();
        var receipt = Assert.IsType<QdosIntakeRecord>(
            await queries.GetAsync(receiptId, CancellationToken.None));
        Assert.Equal(sourceHash, receipt.SourceHash);
        Assert.Equal(IntakeSourceChannel.ManualUpload, receipt.SourceIdentity.Channel);
        Assert.Equal(32, receipt.SourceIdentity.ExternalReceiptToken.Length);
        Assert.Equal("legacy-evidence", Assert.Single(receipt.Evidence).Signal);
        Assert.Equal("LEGACY-SQLITE-001", Assert.Single(receipt.Fields).SuggestedValue);
        var typed = Assert.IsType<QdosTypedDraft>(receipt.TypedDraft);
        Assert.Equal("QDOS", typed.PrincipalCode);
        Assert.Null(typed.ClaimantName);
        Assert.Null(typed.ClaimNumber);
        Assert.Null(typed.VehicleRegistration);
        Assert.Equal(assetHash, Assert.Single(receipt.AssetRecords).ContentHash);

        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(4, await ScalarAsync<long>(context,
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\""));
        Assert.Equal(1, await ScalarAsync<long>(context,
            "SELECT COUNT(*) FROM \"AuditEvents\" WHERE \"EventType\" = 'LegacyReceiptRecorded' " +
            "AND \"DetailsJson\" = '{\"legacy\":\"history\"}'"));
        var preservedJson = await ScalarAsync<string>(context,
            "SELECT \"DetailsJson\" FROM \"AuditEvents\" " +
            "WHERE \"EventType\" = 'RetiredLocalCaseAllocationPreserved'");
        using var preserved = JsonDocument.Parse(preservedJson);
        Assert.Equal(1, preserved.RootElement.GetProperty("retiredLocalProof").GetInt32());
        Assert.Equal("QDOS", preserved.RootElement.GetProperty("principalCode").GetString());
        Assert.Equal("QDOS31043", preserved.RootElement.GetProperty("caseReference").GetString());
        Assert.Equal(2031, preserved.RootElement.GetProperty("counterYear").GetInt32());
        Assert.Equal(43, preserved.RootElement.GetProperty("counterCurrentSequence").GetInt32());
        Assert.Equal(0, await ScalarAsync<long>(context,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' " +
            "AND name IN ('Cases', 'PrincipalYearCounters')"));
        Assert.Equal(0, await ScalarAsync<long>(context,
            "SELECT COUNT(*) FROM pragma_table_info('QdosIntakeReceipts') WHERE name = 'CaseId'"));
        Assert.Equal(0, await ScalarAsync<long>(context,
            "SELECT COUNT(*) FROM pragma_table_info('AuditEvents') WHERE name = 'CaseId'"));
        Assert.Equal(1, await ScalarAsync<long>(context, "PRAGMA foreign_keys"));
        Assert.Equal(0, await ScalarAsync<long>(context, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
        Assert.True(File.Exists(factory.DatabasePath));
    }

    private static async Task CreateAssetSchemaWithoutHistoryAsync(
        string databasePath,
        Guid receiptId,
        Guid caseId,
        Guid assetId,
        string sourceHash,
        string assetHash,
        string legacyEvidence,
        string legacyFields,
        string legacyAuditDetails)
    {
        var services = new ServiceCollection();
        services.AddCollisionSpikeInfrastructure((_, options) =>
            options.UseSqlite($"Data Source={databasePath}"));
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var contextFactory = provider.GetRequiredService<IDbContextFactory<CollisionSpikeDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.GetService<IMigrator>().MigrateAsync("20260723125212_AddIntakeAssets");
        await ExecuteAsync(context, $"""
            INSERT INTO "Cases" ("Id", "PrincipalCode", "CaseReference", "CreatedAtUtc")
            VALUES ('{caseId:D}', 'QDOS', 'QDOS31043', '2031-05-06 10:30:00+00:00');
            INSERT INTO "PrincipalYearCounters" ("PrincipalCode", "Year", "CurrentSequence")
            VALUES ('QDOS', 2031, 43);
            INSERT INTO "QdosIntakeReceipts"
                ("Id", "SourceFileName", "MediaType", "SourceLength", "SourceHash", "ReceivedAtUtc",
                 "Decision", "DecisionReason", "EvidenceJson", "FieldsJson", "FailureCode", "FailureReason",
                 "CaseId", "OcrCandidatesJson")
            VALUES
                ('{receiptId:D}', 'legacy-sqlite.eml', 'message/rfc822', 654, '{sourceHash}',
                 '2031-05-06 10:30:00+00:00', 'ConfirmedQdos', 'Legacy SQLite confirmed receipt',
                 '{legacyEvidence}', '{legacyFields}', NULL, NULL, '{caseId:D}', '[]');
            INSERT INTO "QdosIntakeAssets"
                ("Id", "IntakeReceiptId", "SourceLabel", "FileName", "MediaType", "Kind", "Disposition",
                 "ContentLength", "ContentHash", "StorageKey", "PageNumber", "BoundsJson", "WidthPixels", "HeightPixels")
            VALUES
                ('{assetId:D}', '{receiptId:D}', 'legacy source', 'legacy-sqlite.eml', 'message/rfc822',
                 'Source', 'Source', 654, '{assetHash}', 'sha256/DD/{assetHash}', NULL, NULL, NULL, NULL);
            INSERT INTO "AuditEvents"
                ("Id", "IntakeReceiptId", "CaseId", "EventType", "Actor", "OccurredAtUtc", "DetailsJson")
            VALUES
                ('54000000-0000-0000-0000-000000000004', '{receiptId:D}', '{caseId:D}',
                 'LegacyReceiptRecorded', 'legacy-test', '2031-05-06 10:30:00+00:00',
                  '{legacyAuditDetails}');
            DROP TABLE "__EFMigrationsHistory";
            """);
    }

    private static async Task ExecuteAsync(CollisionSpikeDbContext context, string commandText)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<T> ScalarAsync<T>(CollisionSpikeDbContext context, string commandText)
    {
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = commandText;
            var value = await command.ExecuteScalarAsync();
            Assert.NotNull(value);
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
