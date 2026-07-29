using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

public static class DevelopmentSqliteBaselineGuard
{
    private static readonly Dictionary<string, ColumnDefinition[]> ExpectedColumns =
        new Dictionary<string, ColumnDefinition[]>(StringComparer.Ordinal)
        {
            ["__EFMigrationsLock"] =
            [
                new("Id", "INTEGER", true, 1),
                new("Timestamp", "TEXT", true, 0)
            ],
            ["__EFMigrationsHistory"] =
            [
                new("MigrationId", "TEXT", true, 1),
                new("ProductVersion", "TEXT", true, 0)
            ],
            ["IntakeReceipts"] =
            [
                new("Id", "TEXT", true, 1),
                new("SourceFileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("SourceLength", "INTEGER", true, 0),
                new("SourceHash", "TEXT", true, 0),
                new("SourceChannel", "TEXT", true, 0),
                new("ExternalReceiptToken", "TEXT", true, 0),
                new("ReceivedAtUtc", "TEXT", true, 0),
                new("ProcessedAtUtc", "TEXT", true, 0),
                new("SourceReaderKey", "TEXT", true, 0),
                new("SourceReaderVersion", "TEXT", true, 0),
                new("ExtractionPolicyKey", "TEXT", false, 0),
                new("ExtractionPolicyVersion", "INTEGER", false, 0),
                new("Decision", "TEXT", true, 0),
                new("DecisionReason", "TEXT", true, 0),
                new("EvidenceJson", "TEXT", true, 0),
                new("FieldsJson", "TEXT", true, 0),
                new("FailureCode", "TEXT", false, 0),
                new("FailureReason", "TEXT", false, 0),
                new("OcrCandidatesJson", "TEXT", true, 0)
            ],
            ["InstructionDrafts"] =
            [
                new("IntakeReceiptId", "TEXT", true, 1),
                new("SuggestedPrincipalCode", "TEXT", false, 0),
                new("ClaimantName", "TEXT", false, 0),
                new("ClaimNumber", "TEXT", false, 0),
                new("VehicleRegistration", "TEXT", false, 0),
                new("VehicleMake", "TEXT", false, 0),
                new("VehicleModel", "TEXT", false, 0),
                new("VehicleMileage", "INTEGER", false, 0),
                new("AccidentCircumstances", "TEXT", false, 0),
                new("DateOfIncident", "date", false, 0),
                new("InstructionDate", "date", false, 0),
                new("InspectionAddress", "TEXT", false, 0)
            ],
            ["IntakeAssets"] =
            [
                new("Id", "TEXT", true, 1),
                new("IntakeReceiptId", "TEXT", true, 0),
                new("SourceLabel", "TEXT", true, 0),
                new("FileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("Kind", "TEXT", true, 0),
                new("Disposition", "TEXT", true, 0),
                new("ContentLength", "INTEGER", true, 0),
                new("ContentHash", "TEXT", true, 0),
                new("StorageKey", "TEXT", true, 0),
                new("PageNumber", "INTEGER", false, 0),
                new("BoundsJson", "TEXT", false, 0),
                new("WidthPixels", "INTEGER", false, 0),
                new("HeightPixels", "INTEGER", false, 0)
            ],
            ["IntakeReceiptEvents"] =
            [
                new("Id", "TEXT", true, 1),
                new("IntakeReceiptId", "TEXT", true, 0),
                new("EventType", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("OccurredAtUtc", "TEXT", true, 0),
                new("DetailsJson", "TEXT", true, 0)
            ]
            ,
            ["IntakeStagedReceipts"] =
            [
                new("Id", "TEXT", true, 1),
                new("SourceFileName", "TEXT", true, 0),
                new("MediaType", "TEXT", true, 0),
                new("SourceLength", "INTEGER", true, 0),
                new("SourceHash", "TEXT", true, 0),
                new("SourceChannel", "TEXT", true, 0),
                new("ExternalReceiptToken", "TEXT", true, 0),
                new("ReceivedAtUtc", "TEXT", true, 0),
                new("Actor", "TEXT", true, 0),
                new("StorageKey", "TEXT", true, 0),
                new("StagedAtUtc", "TEXT", true, 0)
            ],
            ["IntakeWorkItems"] =
            [
                new("Id", "TEXT", true, 1),
                new("StagedReceiptId", "TEXT", true, 0),
                new("OperationKey", "TEXT", true, 0),
                new("State", "TEXT", true, 0),
                new("AttemptCount", "INTEGER", true, 0),
                new("DueAtUtc", "TEXT", true, 0),
                new("LeaseToken", "TEXT", false, 0),
                new("LeaseExpiresAtUtc", "TEXT", false, 0),
                new("ProcessedReceiptId", "TEXT", false, 0),
                new("FailureCode", "TEXT", false, 0),
                new("CompletedAtUtc", "TEXT", false, 0)
            ]
            ,
            ["IntakeEvaluations"] =
            [
                new("Id", "TEXT", true, 1),
                new("StagedReceiptId", "TEXT", true, 0),
                new("ProcessedReceiptId", "TEXT", true, 0),
                new("Revision", "INTEGER", true, 0),
                new("EvaluatedAtUtc", "TEXT", true, 0)
            ]
            ,
            ["ProviderDomainPackages"] =
            [
                new("Version", "TEXT", true, 1),
                new("SchemaVersion", "INTEGER", true, 0),
                new("PackageSha256", "TEXT", true, 0),
                new("SourcePath", "TEXT", true, 0),
                new("SourceContentSha256", "TEXT", true, 0),
                new("SourceSheet", "TEXT", true, 0),
                new("SourceRowCount", "INTEGER", true, 0)
            ],
            ["ProviderReferences"] =
            [
                new("Version", "TEXT", true, 1),
                new("Code", "TEXT", true, 2),
                new("SourceRow", "INTEGER", true, 0)
            ],
            ["ProviderDomainEvidence"] =
            [
                new("Version", "TEXT", true, 1),
                new("Code", "TEXT", true, 2),
                new("DomainSuffix", "TEXT", true, 3)
            ]
        };

    private static readonly Dictionary<string, IndexDefinition[]> ExpectedIndexes =
        new Dictionary<string, IndexDefinition[]>(StringComparer.Ordinal)
        {
            ["__EFMigrationsLock"] = [],
            ["__EFMigrationsHistory"] = [new(null, true, "pk", ["MigrationId"])],
            ["IntakeReceipts"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeReceipts_SourceChannel_ExternalReceiptToken", true, "c", ["SourceChannel", "ExternalReceiptToken"]),
                new("IX_IntakeReceipts_SourceHash", false, "c", ["SourceHash"])
            ],
            ["InstructionDrafts"] = [new(null, true, "pk", ["IntakeReceiptId"])],
            ["IntakeAssets"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeAssets_IntakeReceiptId_ContentHash", false, "c", ["IntakeReceiptId", "ContentHash"])
            ],
            ["IntakeReceiptEvents"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeReceiptEvents_IntakeReceiptId", false, "c", ["IntakeReceiptId"])
            ]
            ,
            ["IntakeStagedReceipts"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeStagedReceipts_SourceChannel_ExternalReceiptToken", true, "c", ["SourceChannel", "ExternalReceiptToken"]),
                new("IX_IntakeStagedReceipts_SourceHash", false, "c", ["SourceHash"])
            ],
            ["IntakeWorkItems"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeWorkItems_OperationKey", true, "c", ["OperationKey"]),
                new("IX_IntakeWorkItems_StagedReceiptId", true, "c", ["StagedReceiptId"]),
                new("IX_IntakeWorkItems_State_DueAtUtc", false, "c", ["State", "DueAtUtc"])
            ]
            ,
            ["IntakeEvaluations"] =
            [
                new(null, true, "pk", ["Id"]),
                new("IX_IntakeEvaluations_StagedReceiptId_Revision", true, "c", ["StagedReceiptId", "Revision"])
            ]
            ,
            ["ProviderDomainPackages"] = [new(null, true, "pk", ["Version"])],
            ["ProviderReferences"] = [new(null, true, "pk", ["Version", "Code"])],
            ["ProviderDomainEvidence"] =
            [
                new(null, true, "pk", ["Version", "Code", "DomainSuffix"]),
                new("IX_ProviderDomainEvidence_Version_DomainSuffix", false, "c", ["Version", "DomainSuffix"])
            ]
        };

    private static readonly Dictionary<string, ForeignKeyDefinition[]> ExpectedForeignKeys =
        new Dictionary<string, ForeignKeyDefinition[]>(StringComparer.Ordinal)
        {
            ["__EFMigrationsLock"] = [],
            ["__EFMigrationsHistory"] = [],
            ["IntakeReceipts"] = [],
            ["InstructionDrafts"] = [new("IntakeReceiptId", "IntakeReceipts", "Id", "CASCADE")],
            ["IntakeAssets"] = [new("IntakeReceiptId", "IntakeReceipts", "Id", "CASCADE")],
            ["IntakeReceiptEvents"] = [new("IntakeReceiptId", "IntakeReceipts", "Id", "RESTRICT")]
            ,
            ["IntakeStagedReceipts"] = [],
            ["IntakeWorkItems"] = [new("StagedReceiptId", "IntakeStagedReceipts", "Id", "RESTRICT")]
            ,
            ["IntakeEvaluations"] = [new("StagedReceiptId", "IntakeStagedReceipts", "Id", "RESTRICT")]
            ,
            ["ProviderDomainPackages"] = [],
            ["ProviderReferences"] = [new("Version", "ProviderDomainPackages", "Version", "RESTRICT")],
            ["ProviderDomainEvidence"] =
            [
                new("Code", "ProviderReferences", "Code", "RESTRICT"),
                new("Version", "ProviderReferences", "Version", "RESTRICT")
            ]
        };

    public static async Task ValidateAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Database.IsSqlite())
        {
            throw new InvalidOperationException("The Development baseline guard supports only SQLite.");
        }

        var migrations = context.Database.GetMigrations().ToArray();
        if (migrations.Length != 3)
        {
            throw new InvalidOperationException(
                $"The Development SQLite baseline requires exactly three current migrations; found {migrations.Length}.");
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
            if (tables.Count == 0)
            {
                return;
            }

            if (!tables.SetEquals(ExpectedColumns.Keys))
            {
                throw IncompatibleSchema("table set");
            }

            var history = await ReadMigrationHistoryAsync(connection, cancellationToken);
            if (history.Count != migrations.Length)
            {
                throw IncompatibleSchema("migration history");
            }
            for (var index = 0; index < migrations.Length; index++)
            {
                if (!string.Equals(history[index].MigrationId, migrations[index], StringComparison.Ordinal)
                    || !string.Equals(history[index].ProductVersion, "10.0.10", StringComparison.Ordinal))
                {
                    throw IncompatibleSchema("migration history");
                }
            }

            foreach (var table in ExpectedColumns.Keys)
            {
                var columns = await ReadColumnsAsync(connection, table, cancellationToken);
                if (!columns.SequenceEqual(ExpectedColumns[table]))
                {
                    throw IncompatibleSchema($"columns for {table}");
                }

                var indexes = await ReadIndexesAsync(connection, table, cancellationToken);
                if (!EquivalentIndexes(indexes, ExpectedIndexes[table]))
                {
                    throw IncompatibleSchema($"indexes for {table}");
                }

                var foreignKeys = await ReadForeignKeysAsync(connection, table, cancellationToken);
                if (!foreignKeys.SequenceEqual(ExpectedForeignKeys[table]))
                {
                    throw IncompatibleSchema($"foreign keys for {table}");
                }
            }
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
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<List<MigrationHistory>> ReadMigrationHistoryAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT \"MigrationId\", \"ProductVersion\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<MigrationHistory>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(reader.GetString(0), reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<ColumnDefinition[]> ReadColumnsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<(int Ordinal, ColumnDefinition Definition)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add((
                reader.GetInt32(0),
                new(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) == 1,
                    reader.GetInt32(5))));
        }

        return columns.OrderBy(item => item.Ordinal).Select(item => item.Definition).ToArray();
    }

    private static async Task<IndexDefinition[]> ReadIndexesAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var indexes = new List<IndexDefinition>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(string Name, bool Unique, string Origin)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((reader.GetString(1), reader.GetInt32(2) == 1, reader.GetString(3)));
        }

        await reader.DisposeAsync();
        foreach (var row in rows)
        {
            await using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText = $"PRAGMA index_info(\"{row.Name}\");";
            await using var columnReader = await columnCommand.ExecuteReaderAsync(cancellationToken);
            var columns = new List<(int Ordinal, string Name)>();
            while (await columnReader.ReadAsync(cancellationToken))
            {
                columns.Add((columnReader.GetInt32(0), columnReader.GetString(2)));
            }

            indexes.Add(new(
                row.Origin == "c" ? row.Name : null,
                row.Unique,
                row.Origin,
                columns.OrderBy(item => item.Ordinal).Select(item => item.Name).ToArray()));
        }

        return indexes.ToArray();
    }

    private static async Task<ForeignKeyDefinition[]> ReadForeignKeysAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var keys = new List<ForeignKeyDefinition>();
        while (await reader.ReadAsync(cancellationToken))
        {
            keys.Add(new(reader.GetString(3), reader.GetString(2), reader.GetString(4), reader.GetString(6)));
        }

        return keys
            .OrderBy(item => item.From, StringComparer.Ordinal)
            .ThenBy(item => item.Table, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool EquivalentIndexes(
        IReadOnlyList<IndexDefinition> actual,
        IReadOnlyList<IndexDefinition> expected) =>
        actual.Count == expected.Count
        && expected.All(expectedIndex => actual.Any(actualIndex =>
            string.Equals(actualIndex.Name, expectedIndex.Name, StringComparison.Ordinal)
            && actualIndex.Unique == expectedIndex.Unique
            && string.Equals(actualIndex.Origin, expectedIndex.Origin, StringComparison.Ordinal)
            && actualIndex.Columns.SequenceEqual(expectedIndex.Columns)));

    private static InvalidOperationException IncompatibleSchema(string mismatch) => new(
        $"The local SQLite database does not exactly match the current Development baseline ({mismatch}). " +
        "The database was left unchanged; use the new configured Development database path.");

    private sealed record MigrationHistory(string MigrationId, string ProductVersion);
    private sealed record ColumnDefinition(string Name, string Type, bool NotNull, int PrimaryKeyOrdinal);
    private sealed record IndexDefinition(string? Name, bool Unique, string Origin, string[] Columns);
    private sealed record ForeignKeyDefinition(string From, string Table, string To, string OnDelete);
}
