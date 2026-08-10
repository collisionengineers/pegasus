using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Pegasus.Infrastructure.Maintenance;

internal sealed class CleanBaselineSqlStore : ICleanBaselineSqlStore
{
    private static readonly HashSet<string> DeletableTables = new(StringComparer.Ordinal)
    {
        "ApprovedInboxPoisonMessages",
        "InstructionDrafts",
        "IntakeAssets",
        "IntakeCaseMatchDecisions",
        "IntakeEvaluations",
        "IntakeMailClassificationDecisions",
        "IntakeMailRouteDecisions",
        "IntakeReceiptEvents",
        "IntakeReceipts",
        "IntakeStagedReceipts",
        "IntakeWorkItems",
        "RetainedMailboxAttachments",
        "RetainedMailboxMessages"
    };

    private readonly string connectionString;
    private readonly Func<CancellationToken, Task<string?>> accessToken;

    internal CleanBaselineSqlStore(
        string connectionString,
        Func<CancellationToken, Task<string?>> accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
        this.accessToken = accessToken;
    }

    internal static CleanBaselineSqlStore ForLocalFixture(string connectionString) =>
        new(connectionString, _ => Task.FromResult<string?>(null));

    public async Task<CleanBaselineSqlInventory> InventoryAsync(
        DateTimeOffset cutoffUtc,
        string mailboxIdentity,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await InventoryCoreAsync(
            connection,
            transaction: null,
            cutoffUtc,
            mailboxIdentity,
            cancellationToken);
    }

    private static async Task<CleanBaselineSqlInventory> InventoryCoreAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        DateTimeOffset cutoffUtc,
        string mailboxIdentity,
        CancellationToken cancellationToken)
    {
        var metadata = await ReadMetadataAsync(connection, transaction, cancellationToken);
        var roots = new List<DbRow>();
        var stops = new List<CleanBaselineStopCondition>();

        roots.AddRange(await ReadRootsAsync(
            connection,
            metadata,
            "IntakeReceipts",
            "ReceivedAtUtc < @cutoff AND SourceChannel IN (N'mailbox', N'manual_upload')",
            [new("@cutoff", cutoffUtc)],
            transaction,
            cancellationToken));
        roots.AddRange(await ReadRootsAsync(
            connection,
            metadata,
            "IntakeStagedReceipts",
            "ReceivedAtUtc < @cutoff AND SourceChannel IN (N'mailbox', N'manual_upload')",
            [new("@cutoff", cutoffUtc)],
            transaction,
            cancellationToken));
        roots.AddRange(await ReadRootsAsync(
            connection,
            metadata,
            "RetainedMailboxMessages",
            "ReceivedAtUtc < @cutoff AND MailboxId = @mailbox",
            [new("@cutoff", cutoffUtc), new("@mailbox", mailboxIdentity)],
            transaction,
            cancellationToken));
        roots.AddRange(await ReadRootsAsync(
            connection,
            metadata,
            "ApprovedInboxPoisonMessages",
            "ReceivedAtUtc < @cutoff AND MailboxId = @mailbox",
            [new("@cutoff", cutoffUtc), new("@mailbox", mailboxIdentity)],
            transaction,
            cancellationToken));

        var nonTargetCount = await ScalarAsync<int>(
            connection,
            """
            SELECT
              (SELECT COUNT(*) FROM dbo.IntakeReceipts
               WHERE ReceivedAtUtc < @cutoff
                 AND SourceChannel NOT IN (N'mailbox', N'manual_upload'))
              +
              (SELECT COUNT(*) FROM dbo.IntakeStagedReceipts
               WHERE ReceivedAtUtc < @cutoff
                 AND SourceChannel NOT IN (N'mailbox', N'manual_upload'));
            """,
            [new("@cutoff", cutoffUtc)],
            transaction,
            cancellationToken);
        if (nonTargetCount > 0)
        {
            stops.Add(IntakeCleanBaselineService.Stop(
                "non_target_channel",
                "Sql",
                $"pre-test-non-target:{nonTargetCount}",
                "Pre-test intake rows exist outside mailbox/manual-upload channels."));
        }

        var rows = new Dictionary<string, (DbRow Row, int Depth)>(StringComparer.Ordinal);
        var pending = new Queue<(DbRow Row, int Depth)>();
        foreach (var root in roots)
        {
            pending.Enqueue((root, 0));
        }

        while (pending.TryDequeue(out var item))
        {
            var identity = Identity(item.Row);
            if (rows.TryGetValue(identity, out var existing))
            {
                if (item.Depth > existing.Depth)
                {
                    rows[identity] = (item.Row, item.Depth);
                }
                continue;
            }
            rows.Add(identity, item);

            var classification = Classify(item.Row.Table.Name);
            if (classification != "delete")
            {
                stops.Add(IntakeCleanBaselineService.Stop(
                    classification,
                    "SqlRow",
                    identity,
                    $"{item.Row.Table.Schema}.{item.Row.Table.Name} is a retained or unenumerated dependent."));
                continue;
            }

            foreach (var foreignKey in metadata.ForeignKeys
                         .Where(value => value.Parent == item.Row.Table.Identity))
            {
                var values = foreignKey.Columns
                    .Select(column => item.Row.Values[column.ParentColumn])
                    .ToArray();
                var children = await ReadChildrenAsync(
                    connection,
                    metadata.Tables[foreignKey.Child],
                    foreignKey,
                    values,
                    transaction,
                    cancellationToken);
                foreach (var child in children)
                {
                    pending.Enqueue((child, item.Depth + 1));
                }
            }
        }

        var projected = rows.Values
            .Select(item => Project(item.Row, item.Depth))
            .OrderByDescending(item => item.DependencyDepth)
            .ThenBy(item => item.Schema, StringComparer.Ordinal)
            .ThenBy(item => item.Table, StringComparer.Ordinal)
            .ThenBy(IntakeCleanBaselineService.RowIdentity, StringComparer.Ordinal)
            .ToArray();
        var targetIds = rows.Values
            .Where(item => item.Row.Table.Name == "IntakeStagedReceipts")
            .Select(item => (Guid)item.Row.Values["Id"]!)
            .ToHashSet();
        var targetBlobReferences = rows.Values
            .Select(item => item.Row)
            .Where(item => item.Table.Name is
                "IntakeStagedReceipts" or "IntakeAssets" or "ApprovedInboxPoisonMessages")
            .Select(item => item.Values.TryGetValue("StorageKey", out var key) ? key as string : null)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .GroupBy(key => key!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var references = new Dictionary<string, (int Total, int Target)>(StringComparer.Ordinal);
        foreach (var (key, targetCount) in targetBlobReferences)
        {
            var totalCount = await ScalarAsync<int>(
                connection,
                """
                SELECT
                  (SELECT COUNT(*) FROM dbo.IntakeStagedReceipts WHERE StorageKey = @key)
                  + (SELECT COUNT(*) FROM dbo.IntakeAssets WHERE StorageKey = @key)
                  + (SELECT COUNT(*) FROM dbo.ApprovedInboxPoisonMessages WHERE StorageKey = @key);
                """,
                [new("@key", key)],
                transaction,
                cancellationToken);
            references.Add(key, (totalCount, targetCount));
        }

        return new(projected, references, targetIds, stops);
    }

    public async Task<int> DeleteExactRowsAsync(
        IReadOnlyList<CleanBaselineSqlRow> rows,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var deleted = 0;
        try
        {
            deleted = await DeleteExactRowsCoreAsync(connection, transaction, rows, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<int> DeleteExactRowsCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyList<CleanBaselineSqlRow> rows,
        CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var row in rows
                     .OrderByDescending(item => item.DependencyDepth)
                     .ThenBy(item => item.Schema, StringComparer.Ordinal)
                     .ThenBy(item => item.Table, StringComparer.Ordinal))
        {
            if (!DeletableTables.Contains(row.Table)
                || !row.Schema.Equals("dbo", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The manifest includes a non-deletable SQL table {row.Schema}.{row.Table}.");
            }
            var observed = await ReadExactRowAsync(connection, transaction, row, cancellationToken);
            if (observed is null)
            {
                throw new InvalidOperationException(
                    $"SQL row identity drift detected for {IntakeCleanBaselineService.RowIdentity(row)}.");
            }
            if (!string.Equals(observed.RowSha256, row.RowSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"SQL row drift detected for {IntakeCleanBaselineService.RowIdentity(row)}.");
            }
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM {Quote(row.Schema)}.{Quote(row.Table)} WHERE " +
                string.Join(" AND ", row.Key.Select((key, index) => $"{Quote(key.Column)} = @k{index}"));
            AddKeyParameters(delete, row.Key);
            if (await delete.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException("Exact SQL deletion affected an unexpected row count.");
            }
            deleted++;
        }
        if (deleted != rows.Count)
        {
            throw new InvalidOperationException("Exact SQL deletion did not remove every manifest row.");
        }
        return deleted;
    }

    public async Task<int> CountExistingRowsAsync(
        IReadOnlyList<CleanBaselineSqlRow> rows,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await CountExistingRowsCoreAsync(
            connection,
            transaction: null,
            rows,
            cancellationToken);
    }

    private static async Task<int> CountExistingRowsCoreAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        IReadOnlyList<CleanBaselineSqlRow> rows,
        CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var row in rows)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT COUNT(*) FROM {Quote(row.Schema)}.{Quote(row.Table)} WHERE " +
                string.Join(" AND ", row.Key.Select((key, index) => $"{Quote(key.Column)} = @k{index}"));
            AddKeyParameters(command, row.Key);
            count += Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }
        return count;
    }

    public async Task<string> ReadPollCursorHashAsync(
        string mailboxIdentity,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadPollCursorHashCoreAsync(
            connection,
            transaction: null,
            mailboxIdentity,
            cancellationToken);
    }

    private static async Task<string> ReadPollCursorHashCoreAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string mailboxIdentity,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT [Cursor] FROM dbo.ApprovedInboxPollStates WHERE MailboxId = @mailbox";
        command.Parameters.AddWithValue("@mailbox", mailboxIdentity);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null)
        {
            throw new InvalidOperationException("The exact Inbox poll-state row does not exist.");
        }
        return IntakeCleanBaselineService.Sha256(value is DBNull ? "<null>" : (string)value);
    }

    public async Task WritePollCursorAsync(
        string mailboxIdentity,
        string expectedCursorHash,
        string nextCursor,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nextCursor);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            await WritePollCursorCoreAsync(
                connection,
                transaction,
                mailboxIdentity,
                expectedCursorHash,
                nextCursor,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task WritePollCursorCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string mailboxIdentity,
        string expectedCursorHash,
        string nextCursor,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nextCursor);
        await using var read = connection.CreateCommand();
        read.Transaction = transaction;
        read.CommandText =
            "SELECT [Cursor] FROM dbo.ApprovedInboxPollStates WITH (UPDLOCK, HOLDLOCK) " +
            "WHERE MailboxId = @mailbox";
        read.Parameters.AddWithValue("@mailbox", mailboxIdentity);
        var value = await read.ExecuteScalarAsync(cancellationToken);
        if (value is null)
        {
            throw new InvalidOperationException("The exact Inbox poll-state row does not exist.");
        }
        var currentHash = IntakeCleanBaselineService.Sha256(
            value is DBNull ? "<null>" : (string)value);
        var nextHash = IntakeCleanBaselineService.Sha256(nextCursor);
        if (string.Equals(currentHash, nextHash, StringComparison.Ordinal))
        {
            return;
        }
        if (!string.Equals(currentHash, expectedCursorHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Inbox poll cursor drifted before update.");
        }
        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            "UPDATE dbo.ApprovedInboxPollStates SET [Cursor] = @cursor WHERE MailboxId = @mailbox";
        update.Parameters.AddWithValue("@cursor", nextCursor);
        update.Parameters.AddWithValue("@mailbox", mailboxIdentity);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("The exact Inbox poll-state update failed.");
        }
    }

    public async Task<CleanBaselineRetainedFingerprint> ReadRetainedFingerprintAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await ReadRetainedFingerprintCoreAsync(
            connection,
            transaction: null,
            cancellationToken);
    }

    private static async Task<CleanBaselineRetainedFingerprint> ReadRetainedFingerprintCoreAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var cases = await FingerprintAsync(
            connection,
            "SELECT CONVERT(nvarchar(36), Id) + N'|' + Reference FROM dbo.Cases ORDER BY Id",
            transaction,
            cancellationToken);
        var triage = await FingerprintAsync(
            connection,
            "SELECT CONVERT(nvarchar(36), Id) FROM dbo.Triage ORDER BY Id",
            transaction,
            cancellationToken);
        var principals = await FingerprintAsync(
            connection,
            "SELECT CONVERT(nvarchar(36), Id) + N'|' + Code FROM dbo.Principals ORDER BY Id",
            transaction,
            cancellationToken);
        return new(cases.Count, cases.Hash, triage.Count, triage.Hash, principals.Count, principals.Hash);
    }

    public async Task<ICleanBaselineSqlExecution> BeginLockedExecutionAsync(
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);
        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        try
        {
            await AcquireAllUserTableLocksAsync(connection, transaction, cancellationToken);
            return new LockedExecution(connection, transaction);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task AcquireAllUserTableLocksAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        var tables = new List<(string Schema, string Table)>();
        await using (var list = connection.CreateCommand())
        {
            list.Transaction = transaction;
            list.CommandText =
                "SELECT s.name, t.name FROM sys.tables t " +
                "JOIN sys.schemas s ON s.schema_id = t.schema_id " +
                "WHERE t.is_ms_shipped = 0 ORDER BY s.name, t.name";
            await using var reader = await list.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        foreach (var (schema, table) in tables)
        {
            await using var acquire = connection.CreateCommand();
            acquire.Transaction = transaction;
            acquire.CommandText =
                $"SELECT TOP (1) 1 FROM {Quote(schema)}.{Quote(table)} WITH (TABLOCKX, HOLDLOCK)";
            await acquire.ExecuteScalarAsync(cancellationToken);
        }
    }

    private sealed class LockedExecution(
        SqlConnection connection,
        SqlTransaction transaction) : ICleanBaselineSqlExecution
    {
        private bool committed;

        public Task<CleanBaselineSqlInventory> InventoryAsync(
            DateTimeOffset cutoffUtc,
            string mailboxIdentity,
            CancellationToken cancellationToken) => InventoryCoreAsync(
                connection,
                transaction,
                cutoffUtc,
                mailboxIdentity,
                cancellationToken);

        public Task<int> DeleteExactRowsAsync(
            IReadOnlyList<CleanBaselineSqlRow> rows,
            CancellationToken cancellationToken) => DeleteExactRowsCoreAsync(
                connection,
                transaction,
                rows,
                cancellationToken);

        public Task<int> CountExistingRowsAsync(
            IReadOnlyList<CleanBaselineSqlRow> rows,
            CancellationToken cancellationToken) => CountExistingRowsCoreAsync(
                connection,
                transaction,
                rows,
                cancellationToken);

        public Task<string> ReadPollCursorHashAsync(
            string mailboxIdentity,
            CancellationToken cancellationToken) => ReadPollCursorHashCoreAsync(
                connection,
                transaction,
                mailboxIdentity,
                cancellationToken);

        public Task WritePollCursorAsync(
            string mailboxIdentity,
            string expectedCursorHash,
            string nextCursor,
            CancellationToken cancellationToken) => WritePollCursorCoreAsync(
                connection,
                transaction,
                mailboxIdentity,
                expectedCursorHash,
                nextCursor,
                cancellationToken);

        public Task<CleanBaselineRetainedFingerprint> ReadRetainedFingerprintAsync(
            CancellationToken cancellationToken) => ReadRetainedFingerprintCoreAsync(
                connection,
                transaction,
                cancellationToken);

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!committed)
            {
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (InvalidOperationException)
                {
                    // The server has already ended the transaction; disposal still releases the connection.
                }
            }
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    internal async Task<IReadOnlyList<string>> ReadEffectiveRolesAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sys.database_principals WHERE type = 'R' AND IS_MEMBER(name) = 1 ORDER BY name";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetString(0));
        }
        return result;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        var token = await accessToken(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            connection.AccessToken = token;
        }
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<SchemaMetadata> ReadMetadataAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string tableQuery = """
            SELECT s.name, t.name, c.name, ty.name, ic.key_ordinal
            FROM sys.tables t
            JOIN sys.schemas s ON s.schema_id = t.schema_id
            JOIN sys.indexes i ON i.object_id = t.object_id AND i.is_primary_key = 1
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
            JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            ORDER BY s.name, t.name, ic.key_ordinal;
            """;
        var tables = new Dictionary<TableIdentity, TableMetadata>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = tableQuery;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var identity = new TableIdentity(reader.GetString(0), reader.GetString(1));
                if (!tables.TryGetValue(identity, out var table))
                {
                    table = new(identity.Schema, identity.Name, []);
                    tables.Add(identity, table);
                }
                table.PrimaryKey.Add(new(reader.GetString(2), reader.GetString(3)));
            }
        }

        const string foreignKeyQuery = """
            SELECT fk.name,
                   ps.name, pt.name, pc.name,
                   cs.name, ct.name, cc.name,
                   fkc.constraint_column_id
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.tables pt ON pt.object_id = fk.referenced_object_id
            JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
            JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.referenced_column_id
            JOIN sys.tables ct ON ct.object_id = fk.parent_object_id
            JOIN sys.schemas cs ON cs.schema_id = ct.schema_id
            JOIN sys.columns cc ON cc.object_id = ct.object_id AND cc.column_id = fkc.parent_column_id
            ORDER BY fk.name, fkc.constraint_column_id;
            """;
        var foreignKeys = new Dictionary<string, ForeignKeyMetadata>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = foreignKeyQuery;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                if (!foreignKeys.TryGetValue(name, out var foreignKey))
                {
                    foreignKey = new(
                        name,
                        new(reader.GetString(1), reader.GetString(2)),
                        new(reader.GetString(4), reader.GetString(5)),
                        []);
                    foreignKeys.Add(name, foreignKey);
                }
                foreignKey.Columns.Add(new(reader.GetString(3), reader.GetString(6)));
            }
        }
        return new(tables, foreignKeys.Values.ToArray());
    }

    private static async Task<IReadOnlyList<DbRow>> ReadRootsAsync(
        SqlConnection connection,
        SchemaMetadata metadata,
        string tableName,
        string predicate,
        IReadOnlyList<SqlParameter> parameters,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var table = metadata.Tables[new("dbo", tableName)];
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT * FROM dbo.{Quote(tableName)} WHERE {predicate}";
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }
        return await ReadRowsAsync(command, table, cancellationToken);
    }

    private static async Task<IReadOnlyList<DbRow>> ReadChildrenAsync(
        SqlConnection connection,
        TableMetadata child,
        ForeignKeyMetadata foreignKey,
        object?[] values,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT * FROM {Quote(child.Schema)}.{Quote(child.Name)} WHERE " +
            string.Join(" AND ", foreignKey.Columns.Select((column, index) =>
                $"{Quote(column.ChildColumn)} = @fk{index}"));
        for (var index = 0; index < values.Length; index++)
        {
            command.Parameters.AddWithValue($"@fk{index}", values[index] ?? DBNull.Value);
        }
        return await ReadRowsAsync(command, child, cancellationToken);
    }

    private static async Task<IReadOnlyList<DbRow>> ReadRowsAsync(
        SqlCommand command,
        TableMetadata table,
        CancellationToken cancellationToken)
    {
        var result = new List<DbRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                values.Add(reader.GetName(index), await reader.IsDBNullAsync(index, cancellationToken)
                    ? null
                    : reader.GetValue(index));
            }
            result.Add(new(table, values));
        }
        return result;
    }

    private static async Task<CleanBaselineSqlRow?> ReadExactRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CleanBaselineSqlRow row,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT * FROM {Quote(row.Schema)}.{Quote(row.Table)} WITH (UPDLOCK, HOLDLOCK) WHERE " +
            string.Join(" AND ", row.Key.Select((key, index) => $"{Quote(key.Column)} = @k{index}"));
        AddKeyParameters(command, row.Key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values.Add(reader.GetName(index), await reader.IsDBNullAsync(index, cancellationToken)
                ? null
                : reader.GetValue(index));
        }
        var table = new TableMetadata(row.Schema, row.Table, row.Key.Select(key => new KeyColumn(key.Column, key.Type)).ToList());
        return Project(new(table, values), row.DependencyDepth);
    }

    private static CleanBaselineSqlRow Project(DbRow row, int depth)
    {
        var key = row.Table.PrimaryKey
            .Select(column => new CleanBaselineKeyValue(
                column.Name,
                column.Type,
                Canonical(row.Values[column.Name])))
            .ToArray();
        var material = string.Join(
            "\n",
            row.Values.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={Canonical(item.Value)}"));
        return new(
            row.Table.Schema,
            row.Table.Name,
            key,
            IntakeCleanBaselineService.Sha256(material),
            depth,
            Classify(row.Table.Name));
    }

    private static string Classify(string table) => table switch
    {
        _ when DeletableTables.Contains(table) => "delete",
        "Cases" or "CaseIntakeLinks" or "IntakeManualAssociations" => "case_link",
        _ when table.StartsWith("Triage", StringComparison.Ordinal) => "triage_link",
        "ImageIntakes" or "ImageVrmSuggestions" => "image_intake_link",
        "DocumentOccurrences" or "DocumentVersions" or "CaseDocuments" or "ExternalWorkItems" => "custody_link",
        _ => "unenumerated_fk_dependent"
    };

    private static string Identity(DbRow row) =>
        $"{row.Table.Schema}.{row.Table.Name}:" + string.Join(
            ",",
            row.Table.PrimaryKey.Select(column => $"{column.Name}={Canonical(row.Values[column.Name])}"));

    private static string Canonical(object? value) => value switch
    {
        null => "<null>",
        byte[] bytes => Convert.ToHexString(bytes),
        Guid guid => guid.ToString("D"),
        DateTimeOffset instant => instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTime instant => instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static async Task<(int Count, string Hash)> FingerprintAsync(
        SqlConnection connection,
        string commandText,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(reader.GetString(0));
        }
        return (values.Count, IntakeCleanBaselineService.Sha256(string.Join("\n", values)));
    }

    private static async Task<T> ScalarAsync<T>(
        SqlConnection connection,
        string commandText,
        IReadOnlyList<SqlParameter> parameters,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }
        var value = await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The SQL scalar returned no value.");
        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static void AddKeyParameters(SqlCommand command, IReadOnlyList<CleanBaselineKeyValue> keys)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            object value = key.Type switch
            {
                "uniqueidentifier" => Guid.Parse(key.Value),
                "int" => int.Parse(key.Value, CultureInfo.InvariantCulture),
                "bigint" => long.Parse(key.Value, CultureInfo.InvariantCulture),
                _ => key.Value
            };
            command.Parameters.AddWithValue($"@k{index}", value);
        }
    }

    private static string Quote(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private sealed record TableIdentity(string Schema, string Name);
    private sealed record KeyColumn(string Name, string Type);
    private sealed record ForeignKeyColumn(string ParentColumn, string ChildColumn);
    private sealed record ForeignKeyMetadata(
        string Name,
        TableIdentity Parent,
        TableIdentity Child,
        List<ForeignKeyColumn> Columns);
    private sealed record TableMetadata(string Schema, string Name, List<KeyColumn> PrimaryKey)
    {
        internal TableIdentity Identity => new(Schema, Name);
    }
    private sealed record SchemaMetadata(
        IReadOnlyDictionary<TableIdentity, TableMetadata> Tables,
        IReadOnlyList<ForeignKeyMetadata> ForeignKeys);
    private sealed record DbRow(TableMetadata Table, IReadOnlyDictionary<string, object?> Values);
}
