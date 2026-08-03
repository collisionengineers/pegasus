using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Proves that restoring the per-run template is indistinguishable from
/// migrating a database, and that the template is built once.
/// </summary>
/// <remarks>
/// Without these, a template that silently failed to build would leave the
/// whole suite green and slow, and a template that restored a stale or partial
/// schema would weaken every other SQL Server test at once.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed class LocalDbTemplateDatabaseTests
{
    [LocalDbTemplateFact]
    public async Task RestoringTheTemplateMatchesMigratingTheDatabase()
    {
        await using var restored = await LocalDbTestDatabase.CreateAsync();
        await using var migrated = await LocalDbTestDatabase.CreateAsync(useTemplate: false);

        Assert.Equal(LocalDbSchemaOrigin.Template, restored.SchemaOrigin);
        Assert.Equal(LocalDbSchemaOrigin.Migrated, migrated.SchemaOrigin);

        await using (var migratedContext = await migrated.CreateContextAsync())
        await using (var restoredContext = await restored.CreateContextAsync())
        {
            var applied = (await migratedContext.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.NotEmpty(applied);
            // A template built from a stale assembly would pass an equality
            // between the two databases alone; the compiled stream is the
            // independent side of this comparison.
            Assert.Equal(migratedContext.Database.GetMigrations().ToArray(), applied);
            Assert.Equal(
                applied,
                (await restoredContext.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await restoredContext.Database.GetPendingMigrationsAsync());
            Assert.False(restoredContext.Database.HasPendingModelChanges());
        }

        Assert.Equal(await ReadStructureAsync(migrated), await ReadStructureAsync(restored));
    }

    [LocalDbTemplateFact]
    public async Task TheTemplateIsBuiltOncePerProcessAndEveryDatabaseIsItsOwn()
    {
        await using var first = await LocalDbTestDatabase.CreateAsync();
        await using var second = await LocalDbTestDatabase.CreateAsync();

        Assert.Equal(LocalDbSchemaOrigin.Template, first.SchemaOrigin);
        Assert.Equal(LocalDbSchemaOrigin.Template, second.SchemaOrigin);
        Assert.NotEqual(first.DatabaseName, second.DatabaseName);
        Assert.Equal(1, LocalDbTemplateDatabase.BuildCount);

        // Restores share one backup file; they must not share a database.
        await first.ExecuteAsync("CREATE TABLE [dbo].[TemplateIsolationProbe] ([Id] int NOT NULL)");

        Assert.Equal(1, await first.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'TemplateIsolationProbe'"));
        Assert.Equal(0, await second.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'TemplateIsolationProbe'"));
    }

    [LocalDbTemplateFact]
    public async Task AnUnmigratedDatabaseIsNeverServedFromTheTemplate()
    {
        await using var empty = await LocalDbTestDatabase.CreateAsync(migrate: false);

        Assert.Equal(LocalDbSchemaOrigin.Empty, empty.SchemaOrigin);
        Assert.Equal(0, await empty.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'__EFMigrationsHistory'"));
    }

    [LocalDbTemplateFact]
    public async Task TheAbandonedDatabaseSweepLeavesALiveDatabaseAlone()
    {
        // The sweep drops databases by name against a shared LocalDB instance,
        // so the suite running in another worktree right now is exactly what
        // its one-day floor has to protect.
        await using var live = await LocalDbTestDatabase.CreateAsync();

        await LocalDbTemplateDatabase.SweepAbandonedDatabasesAsync();

        Assert.Equal(1, await live.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.databases WHERE name = DB_NAME()"));
    }

    [Theory]
    [InlineData("Pegasus_Test_0123456789abcdef0123456789abcdef", true)]
    [InlineData("Pegasus_Test_0123456789ABCDEF0123456789ABCDEF", true)]
    [InlineData("Pegasus", false)]
    [InlineData("Pegasus_Test_", false)]
    [InlineData("Pegasus_Test_0123456789abcdef0123456789abcde", false)]
    [InlineData("Pegasus_Test_0123456789abcdef0123456789abcdef0", false)]
    [InlineData("Pegasus_Test_not-a-guid-not-a-guid-not-a-gu", false)]
    [InlineData("Pegasus_Template_0123456789abcdef0123456789abcd", false)]
    [InlineData("PegasusProduction", false)]
    [InlineData("master", false)]
    public void OnlyAnExactDisposableNameIsEverEligibleToBeDropped(string name, bool disposable) =>
        Assert.Equal(disposable, LocalDbTestDatabase.IsDisposableName(name));

    /// <summary>
    /// Every table, column, index, key, constraint, permission, database
    /// principal, and row count, as one ordered comparable sequence.
    /// </summary>
    /// <remarks>
    /// Rows are assembled and ordered in .NET rather than by <c>CONCAT</c> and
    /// <c>ORDER BY</c>: catalog metadata and a database's own literals carry
    /// different collations, and reconciling them in SQL buys nothing here.
    /// </remarks>
    private static async Task<string[]> ReadStructureAsync(LocalDbTestDatabase database)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        var lines = new List<string>();
        foreach (var (kind, sql) in StructureQueries)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];
                reader.GetValues(values);
                lines.Add(kind + "|" + string.Join(
                    "|",
                    values.Select(value => value is DBNull
                        ? string.Empty
                        : Convert.ToString(value, CultureInfo.InvariantCulture))));
            }
        }

        Assert.NotEmpty(lines);
        lines.Sort(StringComparer.Ordinal);
        return [.. lines];
    }

    private static readonly (string Kind, string Sql)[] StructureQueries =
    [
        ("column",
            """
            SELECT SCHEMA_NAME(t.schema_id), t.name, c.name, c.column_id,
                   TYPE_NAME(c.user_type_id), c.max_length, c.precision, c.scale,
                   c.is_nullable, c.is_identity, c.collation_name, dc.definition
            FROM sys.columns c
            JOIN sys.tables t ON t.object_id = c.object_id
            LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id
                AND dc.parent_column_id = c.column_id
            """),
        ("index",
            """
            SELECT SCHEMA_NAME(t.schema_id), t.name, i.name, i.type_desc, i.is_unique,
                   i.is_primary_key, i.is_unique_constraint, i.filter_definition,
                   (SELECT STRING_AGG(CONCAT(
                               COL_NAME(ic.object_id, ic.column_id) COLLATE DATABASE_DEFAULT,
                               ':', ic.key_ordinal, ':', ic.is_descending_key,
                               ':', ic.is_included_column), ',')
                           WITHIN GROUP (ORDER BY ic.is_included_column, ic.key_ordinal,
                                                  ic.index_column_id)
                      FROM sys.index_columns ic
                     WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id)
            FROM sys.indexes i
            JOIN sys.tables t ON t.object_id = i.object_id
            """),
        ("foreignkey",
            """
            SELECT SCHEMA_NAME(f.schema_id), f.name, OBJECT_NAME(f.parent_object_id),
                   OBJECT_NAME(f.referenced_object_id), f.delete_referential_action_desc,
                   f.update_referential_action_desc,
                   (SELECT STRING_AGG(CONCAT(
                               COL_NAME(fc.parent_object_id, fc.parent_column_id)
                                   COLLATE DATABASE_DEFAULT,
                               '>',
                               COL_NAME(fc.referenced_object_id, fc.referenced_column_id)
                                   COLLATE DATABASE_DEFAULT), ',')
                           WITHIN GROUP (ORDER BY fc.constraint_column_id)
                      FROM sys.foreign_key_columns fc
                     WHERE fc.constraint_object_id = f.object_id)
            FROM sys.foreign_keys f
            """),
        ("check",
            """
            SELECT OBJECT_NAME(cc.parent_object_id), cc.name, cc.definition, cc.is_disabled
            FROM sys.check_constraints cc
            """),
        ("principal",
            """
            SELECT dp.name, dp.type_desc,
                   (SELECT STRING_AGG(USER_NAME(rm.role_principal_id) COLLATE DATABASE_DEFAULT, ',')
                           WITHIN GROUP (ORDER BY USER_NAME(rm.role_principal_id)
                                                  COLLATE DATABASE_DEFAULT)
                      FROM sys.database_role_members rm
                     WHERE rm.member_principal_id = dp.principal_id)
            FROM sys.database_principals dp
            WHERE dp.is_fixed_role = 0
              AND dp.name NOT IN (N'public', N'dbo', N'guest', N'INFORMATION_SCHEMA', N'sys')
            """),
        ("permission",
            """
            SELECT USER_NAME(pe.grantee_principal_id), pe.permission_name, pe.state_desc,
                   pe.class_desc, OBJECT_SCHEMA_NAME(pe.major_id), OBJECT_NAME(pe.major_id),
                   COL_NAME(pe.major_id, NULLIF(pe.minor_id, 0))
            FROM sys.database_permissions pe
            """),
        ("rows",
            """
            SELECT SCHEMA_NAME(t.schema_id), t.name, SUM(p.rows)
            FROM sys.tables t
            JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
            GROUP BY SCHEMA_NAME(t.schema_id), t.name
            """)
    ];
}

/// <summary>
/// A fact that only runs where the template database is expected to work.
/// </summary>
/// <remarks>
/// The template uses server-side <c>BACKUP</c> and <c>RESTORE</c>, which the
/// LocalDB instance the Windows suite uses always permits. No CI job runs the
/// <c>PEGASUS_TEST_SQL_DATASOURCE</c> path, so nothing proves the template
/// there — and for exactly that reason
/// <see cref="LocalDbTemplateDatabase"/> never engages against an external
/// server: it migrates per test instead, which these guards would otherwise
/// flag as a silent fallback.
/// </remarks>
internal sealed class LocalDbTemplateFactAttribute : FactAttribute
{
    public LocalDbTemplateFactAttribute()
    {
        if (LocalDbTestDatabase.UsesExternalDataSource)
        {
            Skip = "PEGASUS_TEST_SQL_DATASOURCE points these tests at an external SQL Server, " +
                "where the template is disabled until a job proves the backup path.";
        }
    }
}
