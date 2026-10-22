namespace Pegasus.IntegrationTests;

/// <summary>
/// Removes one row and every row that reaches it through a foreign key,
/// children before parents, so a test that shares its class's database leaves
/// it as it found it. The keys are read from the database, so a table added
/// later is covered without a change here.
/// </summary>
internal sealed class ForeignKeyRowRemoval
{
    internal const string CasesTable = "[dbo].[Cases]";

    internal const string IntakeReceiptsTable = "[dbo].[IntakeReceipts]";

    /// <summary>Every foreign key, one row for each column pair: its id, child table, parent table, then the columns.</summary>
    private const string ForeignKeysSql = """
        SELECT fk.object_id,
               QUOTENAME(SCHEMA_NAME(child.schema_id)) + N'.' + QUOTENAME(child.name),
               QUOTENAME(SCHEMA_NAME(parent.schema_id)) + N'.' + QUOTENAME(parent.name),
               QUOTENAME(childColumn.name),
               QUOTENAME(parentColumn.name)
        FROM sys.foreign_keys AS fk
        JOIN sys.tables AS child ON child.object_id = fk.parent_object_id
        JOIN sys.tables AS parent ON parent.object_id = fk.referenced_object_id
        JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns AS childColumn
            ON childColumn.object_id = fkc.parent_object_id AND childColumn.column_id = fkc.parent_column_id
        JOIN sys.columns AS parentColumn
            ON parentColumn.object_id = fkc.referenced_object_id AND parentColumn.column_id = fkc.referenced_column_id
        ORDER BY fk.object_id, fkc.constraint_column_id
        """;

    private readonly LocalDbTestDatabase database;
    private readonly string removeSql;

    private ForeignKeyRowRemoval(LocalDbTestDatabase database, string removeSql)
    {
        this.database = database;
        this.removeSql = removeSql;
    }

    /// <summary>
    /// Reads the database's keys and builds the one batch that removes the row
    /// of <paramref name="rootTable"/> named by <c>@id</c> and everything that
    /// reaches it. A table in <paramref name="skippedTables"/>, and what only
    /// it reaches, is left to the caller (it has been removed already).
    /// </summary>
    internal static async Task<ForeignKeyRowRemoval> CreateAsync(
        LocalDbTestDatabase database, string rootTable, params string[] skippedTables)
    {
        var keys = new Dictionary<int, ForeignKeyEdge>();
        await using (var connection = database.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = ForeignKeysSql;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var id = reader.GetInt32(0);
                if (!keys.TryGetValue(id, out var key))
                {
                    keys[id] = key = new ForeignKeyEdge(reader.GetString(1), reader.GetString(2), []);
                }

                key.Columns.Add((reader.GetString(3), reader.GetString(4)));
            }
        }

        // A table that references itself is deleted in one statement, so it needs no edge.
        var edges = keys.Values
            .Where(edge => edge.Child != edge.Parent && !skippedTables.Contains(edge.Child))
            .ToList();
        var tables = new List<string>();
        var reached = new HashSet<string>(StringComparer.Ordinal);

        // Depth first from the root down to the tables that reference it: a table is listed after every table that references it.
        void Visit(string table)
        {
            if (!reached.Add(table))
            {
                return;
            }

            foreach (var edge in edges.Where(candidate => candidate.Parent == table))
            {
                Visit(edge.Child);
            }

            tables.Add(table);
        }

        // The rows of a table that reach the root: a row of the root itself, or a row whose parent is such a row.
        string Reaches(string table, string alias, int depth)
        {
            if (table == rootTable)
            {
                return $"{alias}.[Id] = @id";
            }

            var parentAlias = $"p{depth}";
            var routes = edges
                .Where(edge => edge.Child == table && reached.Contains(edge.Parent))
                .Select(edge => $"EXISTS (SELECT 1 FROM {edge.Parent} AS {parentAlias} "
                    + $"WHERE {Reaches(edge.Parent, parentAlias, depth + 1)} AND "
                    + string.Join(" AND ", edge.Columns.Select(column =>
                        $"{alias}.{column.Child} = {parentAlias}.{column.Parent}")) + ")");
            return "(" + string.Join(" OR ", routes) + ")";
        }

        Visit(rootTable);
        const string rows = "t";
        var sql = string.Join(
            ";\n", tables.Select(table => $"DELETE {rows} FROM {table} AS {rows} WHERE {Reaches(table, rows, 0)}"));
        return new ForeignKeyRowRemoval(database, sql);
    }

    /// <summary>Removes the root row with the given id and everything that reaches it.</summary>
    internal async Task RemoveAsync(Guid id)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = removeSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private sealed record ForeignKeyEdge(string Child, string Parent, List<(string Child, string Parent)> Columns);
}
