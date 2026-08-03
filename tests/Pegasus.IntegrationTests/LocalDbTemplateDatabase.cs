using Microsoft.Data.SqlClient;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The backup of a once-migrated schema that every SQL Server integration test
/// database is restored from.
/// </summary>
internal sealed record LocalDbTemplateSnapshot(
    string BackupPath,
    string DataLogicalName,
    string LogLogicalName,
    string DataDirectory);

/// <summary>
/// Builds the migrated schema exactly once per test-run process.
/// </summary>
/// <remarks>
/// Migrating a fresh database per test dominated the suite's wall clock. The
/// schema is instead migrated once, backed up, and restored per test. Backup
/// and restore are server-side, so LocalDB and the
/// <c>PEGASUS_TEST_SQL_DATASOURCE</c> container behave identically; a failure
/// to build the template falls back to migrating each database, and
/// <see cref="LocalDbTemplateDatabaseTests"/> keeps that fallback from passing
/// silently.
/// </remarks>
internal static class LocalDbTemplateDatabase
{
    private static int buildCount;

    private static readonly Lazy<Task<LocalDbTemplateSnapshot?>> Template =
        new(BuildAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// How many times the template has been built in this process. The
    /// per-run promise is worth nothing if this is ever above one.
    /// </summary>
    internal static int BuildCount => Volatile.Read(ref buildCount);

    public static Task<LocalDbTemplateSnapshot?> GetAsync() => Template.Value;

    private static async Task<LocalDbTemplateSnapshot?> BuildAsync()
    {
        Interlocked.Increment(ref buildCount);
        try
        {
            return await BuildTemplateAsync();
        }
        catch (Exception exception)
        {
            // Falling back to migrate-per-test is correct but slow, so say so
            // loudly rather than leaving a green-and-slow run unexplained.
            await Console.Error.WriteLineAsync(
                "The migrated template database could not be built, so every SQL Server test " +
                $"migrates its own database. {exception}");
            return null;
        }
    }

    private static async Task<LocalDbTemplateSnapshot> BuildTemplateAsync()
    {
        var template = await LocalDbTestDatabase.CreateAsync(useTemplate: false);
        string dataDirectory;
        string backupPath;
        try
        {
            dataDirectory = await ReadDataDirectoryAsync(template);
            backupPath = Combine(dataDirectory, template.DatabaseName + ".bak");
            await BackUpAsync(template, backupPath);
        }
        finally
        {
            // The template database itself is disposable the moment its backup
            // exists; only the backup outlives this method.
            await template.DisposeAsync();
        }

        var (dataLogicalName, logLogicalName) = await ReadLogicalFileNamesAsync(backupPath);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteQuietly(backupPath);
        SweepAbandonedBackups(dataDirectory, backupPath);
        return new(backupPath, dataLogicalName, logLogicalName, dataDirectory);
    }

    private static async Task<string> ReadDataDirectoryAsync(LocalDbTestDatabase template)
    {
        var physicalName = await template.ScalarAsync<string>(
            "SELECT physical_name FROM sys.database_files WHERE type = 0");
        var separator = physicalName.LastIndexOfAny(['\\', '/']);
        Assert.True(separator > 0, $"The template data file has no directory: {physicalName}");
        return physicalName[..separator];
    }

    private static async Task BackUpAsync(LocalDbTestDatabase template, string backupPath)
    {
        await using var connection = template.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"BACKUP DATABASE [{template.DatabaseName}] TO DISK = @backupPath " +
            "WITH INIT, FORMAT, COPY_ONLY";
        command.Parameters.AddWithValue("@backupPath", backupPath);
        command.CommandTimeout = 300;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(string DataLogicalName, string LogLogicalName)>
        ReadLogicalFileNamesAsync(string backupPath)
    {
        await using var connection = new SqlConnection(LocalDbTestDatabase.MasterConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "RESTORE FILELISTONLY FROM DISK = @backupPath";
        command.Parameters.AddWithValue("@backupPath", backupPath);
        await using var reader = await command.ExecuteReaderAsync();
        string? dataLogicalName = null;
        string? logLogicalName = null;
        var logicalNameOrdinal = reader.GetOrdinal("LogicalName");
        var typeOrdinal = reader.GetOrdinal("Type");
        while (await reader.ReadAsync())
        {
            var logicalName = reader.GetString(logicalNameOrdinal);
            switch (char.ToUpperInvariant(reader.GetString(typeOrdinal)[0]))
            {
                case 'D':
                    Assert.Null(dataLogicalName);
                    dataLogicalName = logicalName;
                    break;
                case 'L':
                    Assert.Null(logLogicalName);
                    logLogicalName = logicalName;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"The template backup carries an unexpected file {logicalName}.");
            }
        }

        Assert.NotNull(dataLogicalName);
        Assert.NotNull(logLogicalName);
        return (dataLogicalName, logLogicalName);
    }

    /// <summary>
    /// Joins a server-side path, which is a Windows path on LocalDB and a Linux
    /// path in the SQL Server container regardless of where the tests run.
    /// </summary>
    internal static string Combine(string directory, string fileName)
    {
        var separator = directory.Contains('\\', StringComparison.Ordinal) ? '\\' : '/';
        return directory.TrimEnd('\\', '/') + separator + fileName;
    }

    /// <summary>
    /// Removes backups left behind by a killed run. Best effort: the backup
    /// lives in the server's data directory, which is reachable from the test
    /// process on LocalDB but not when the server is a container.
    /// </summary>
    private static void SweepAbandonedBackups(string dataDirectory, string currentBackupPath)
    {
        try
        {
            if (!Directory.Exists(dataDirectory))
            {
                return;
            }

            var cutoff = DateTime.UtcNow.AddDays(-1);
            foreach (var path in Directory.EnumerateFiles(dataDirectory, "Pegasus_Test_*.bak"))
            {
                if (!string.Equals(path, currentBackupPath, StringComparison.OrdinalIgnoreCase)
                    && File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    DeleteQuietly(path);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
