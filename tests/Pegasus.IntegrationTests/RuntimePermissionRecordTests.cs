namespace Pegasus.IntegrationTests;

/// <summary>
/// The runtime-role permission record,
/// <c>src/Pegasus.Infrastructure/Persistence/RuntimePermissions.csv</c>, is the
/// current statement of what the two runtime roles may do. A fresh database
/// built from the migrations holds exactly its rows, every user table is named
/// in it (a <c>none</c> row for a table neither role touches, which replaces
/// the old per-migration opt-out marker), and the permissions the roles can
/// exercise are exactly the grants it lists. The release bootstrap compares
/// production with the same file.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RuntimePermissionRecordTests : IAsyncLifetime
{
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";
    private static readonly string RecordPath =
        Path.Combine(AppContext.BaseDirectory, "Support", "RuntimePermissions.csv");

    private LocalDbTestDatabase database = null!;

    public async Task InitializeAsync() => database = await LocalDbTestDatabase.CreateAsync();

    public async Task DisposeAsync() => await database.DisposeAsync();

    [Fact]
    public async Task AFreshDatabaseHoldsExactlyTheRecordedPermissions()
    {
        var recorded = ReadRecord().Where(row => row.Role != "none").Select(Row).ToArray();
        var actual = await ReadValuesAsync(
            $"""
            SELECT CONCAT(
                   principal.name COLLATE DATABASE_DEFAULT, N'|',
                   permission.state COLLATE DATABASE_DEFAULT, N'|',
                   permission.permission_name COLLATE DATABASE_DEFAULT, N'|',
                   CASE
                       WHEN permission.class = 1 AND permission.minor_id = 0 AND target.object_id IS NOT NULL
                           THEN target.name COLLATE DATABASE_DEFAULT
                       ELSE CONCAT(N'__UNAPPROVED_SCOPE_', permission.class, N'_', permission.major_id, N'_', permission.minor_id)
                   END)
            FROM sys.database_permissions AS permission
            JOIN sys.database_principals AS principal ON principal.principal_id = permission.grantee_principal_id
            LEFT JOIN sys.tables AS target ON target.object_id = permission.major_id
            WHERE principal.name IN (N'{WebRole}', N'{WorkerRole}')
            """);

        AssertSameRows(recorded, actual, "permission rows");
    }

    [Fact]
    public async Task EveryUserTableIsNamedInTheRecord()
    {
        var named = ReadRecord().Select(row => row.Table).ToHashSet(StringComparer.Ordinal);
        var tables = await ReadValuesAsync("SELECT name FROM sys.tables WHERE is_ms_shipped = 0");

        var unnamed = tables.Where(table => !named.Contains(table)).ToArray();
        Assert.True(
            unnamed.Length == 0,
            "Tables the permission record does not name (add their rows, or a none row when no runtime role touches them): "
            + string.Join(", ", unnamed));
        var gone = named.Where(table => !tables.Contains(table)).ToArray();
        Assert.True(gone.Length == 0, "Tables the permission record names that no longer exist: " + string.Join(", ", gone));
    }

    [Fact]
    public async Task TheRolesCanExerciseExactlyTheRecordedGrants()
    {
        await database.ExecuteAsync(
            $"""
            CREATE USER [pegasus_record_web] WITHOUT LOGIN;
            CREATE USER [pegasus_record_worker] WITHOUT LOGIN;
            ALTER ROLE [{WebRole}] ADD MEMBER [pegasus_record_web];
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_record_worker];
            """);
        var recorded = ReadRecord().Where(row => row.State == "G").Select(Row).ToArray();
        var actual = (await ReadEffectiveAsync("pegasus_record_web", WebRole))
            .Concat(await ReadEffectiveAsync("pegasus_record_worker", WorkerRole))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        AssertSameRows(recorded, actual, "effective grants");
    }

    private Task<string[]> ReadEffectiveAsync(string user, string role) =>
        ReadValuesAsync(
            $"""
            EXECUTE AS USER = N'{user}';
            SELECT CONCAT(N'{role}|G|', candidate.permission_name COLLATE DATABASE_DEFAULT, N'|', target.name COLLATE DATABASE_DEFAULT)
            FROM sys.tables AS target
            CROSS JOIN (VALUES (N'SELECT'), (N'INSERT'), (N'UPDATE'), (N'DELETE')) AS candidate(permission_name)
            WHERE target.is_ms_shipped = 0
              AND HAS_PERMS_BY_NAME(
                  QUOTENAME(SCHEMA_NAME(target.schema_id)) + N'.' + QUOTENAME(target.name),
                  N'OBJECT',
                  candidate.permission_name) = 1;
            REVERT;
            """);

    private static (string Role, string State, string Permission, string Table)[] ReadRecord() =>
        File.ReadAllLines(RecordPath)
            .Skip(1)
            .Where(line => line.Length > 0)
            .Select(line => line.Split(','))
            .Select(parts => (parts[0], parts[1], parts[2], parts[3]))
            .ToArray();

    private static string Row((string Role, string State, string Permission, string Table) row) =>
        $"{row.Role}|{row.State}|{row.Permission}|{row.Table}";

    private static void AssertSameRows(IEnumerable<string> recorded, IEnumerable<string> actual, string what)
    {
        var recordedSet = recorded.ToHashSet(StringComparer.Ordinal);
        var actualSet = actual.ToHashSet(StringComparer.Ordinal);
        var missing = recordedSet.Except(actualSet).Order(StringComparer.Ordinal).ToArray();
        var extra = actualSet.Except(recordedSet).Order(StringComparer.Ordinal).ToArray();
        Assert.True(
            missing.Length == 0 && extra.Length == 0,
            $"The database's {what} differ from RuntimePermissions.csv."
            + Environment.NewLine + "Recorded but absent: " + string.Join(", ", missing)
            + Environment.NewLine + "Present but unrecorded: " + string.Join(", ", extra));
    }

    private async Task<string[]> ReadValuesAsync(string commandText)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }
        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
