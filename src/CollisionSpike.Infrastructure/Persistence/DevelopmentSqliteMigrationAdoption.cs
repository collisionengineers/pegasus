using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace CollisionSpike.Infrastructure.Persistence;

public static class DevelopmentSqliteMigrationAdoption
{
    private const string InitialMigration = "20260723075441_InitialQdosIntake";
    private const string AssetMigration = "20260723125212_AddIntakeAssets";
    private const string TypedDraftMigration = "20260723170000_AddTypedQdosDraftAndSourceIdentity";
    private const string AllocationCleanupMigration = "20260723171000_RemoveRetiredQdosCaseAllocation";
    private const string ProductVersion = "10.0.10";

    public static async Task AdoptEnsureCreatedSchemaAsync(
        CollisionSpikeDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Database.IsSqlite())
        {
            throw new InvalidOperationException("Development schema adoption is supported only for SQLite.");
        }

        var connection = context.Database.GetDbConnection();
        var closeWhenComplete = connection.State == ConnectionState.Closed;
        if (closeWhenComplete)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var tables = await ReadTablesAsync(connection, cancellationToken);
            if (tables.Contains("__EFMigrationsHistory")
                || !tables.Contains("QdosIntakeReceipts"))
            {
                return;
            }

            if (!tables.Contains("AuditEvents"))
            {
                throw IncompatibleSchema();
            }

            var receiptColumns = await ReadColumnsAsync(
                connection,
                "QdosIntakeReceipts",
                cancellationToken);
            var auditColumns = await ReadColumnsAsync(connection, "AuditEvents", cancellationToken);
            var hasCases = tables.Contains("Cases");
            var hasCounters = tables.Contains("PrincipalYearCounters");
            var hasAssets = tables.Contains("QdosIntakeAssets");
            var hasAssetContract = hasAssets && receiptColumns.Contains("OcrCandidatesJson");
            var hasTypedDraft = tables.Contains("QdosTypedDrafts");
            var hasSourceIdentity = receiptColumns.Contains("SourceChannel")
                && receiptColumns.Contains("ExternalReceiptToken");
            var hasLegacyLinks = receiptColumns.Contains("CaseId") && auditColumns.Contains("CaseId");
            var hasCleanupContract = !hasCases && !hasCounters && !hasLegacyLinks;

            if (hasCases != hasCounters
                || hasAssets != receiptColumns.Contains("OcrCandidatesJson")
                || hasTypedDraft != hasSourceIdentity
                || (hasCases && !hasLegacyLinks)
                || (!hasCases && !hasCleanupContract)
                || (hasTypedDraft && !hasAssetContract)
                || (hasCleanupContract && !hasTypedDraft))
            {
                throw IncompatibleSchema();
            }

            var appliedMigrations = new List<string> { InitialMigration };
            if (hasAssetContract)
            {
                appliedMigrations.Add(AssetMigration);
            }

            if (hasTypedDraft)
            {
                appliedMigrations.Add(TypedDraftMigration);
            }

            if (hasCleanupContract)
            {
                appliedMigrations.Add(AllocationCleanupMigration);
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """,
                cancellationToken);

            foreach (var migration in appliedMigrations)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                    "VALUES ($migration, $productVersion);";
                AddParameter(command, "$migration", migration);
                AddParameter(command, "$productVersion", ProductVersion);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (closeWhenComplete)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> ReadTablesAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static InvalidOperationException IncompatibleSchema() => new(
        "The local SQLite database has an unrecognised pre-migration schema. " +
        "Preserve the database file and use a fresh Development database path rather than deleting evidence.");
}
