using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261011112442_DiscardDeletesRepairSpec: discard deletes a repair spec
/// (operator, 11 October 2026), so every spec already discarded goes with its
/// lines and numbered versions, and the discard columns and their constraint
/// go with them. A live spec keeps its lines and versions.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DiscardDeletesRepairSpecMigrationTests
{
    private const string PreviousMigration = "20261009120000_WorkCentreActivityIndexes";
    private const string CaseId = RepairSpecInUseOnCreateMigrationTests.CaseId;
    private const string LiveId = "a7000000-0000-0000-0000-000000000001";
    private const string DiscardedId = "a7000000-0000-0000-0000-000000000002";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    [Fact]
    public async Task MigrationDeletesEveryDiscardedSpecWithItsLinesAndVersions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SpecificationsSql);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        foreach (var (table, column) in new[]
        {
            ("CaseRepairSpecifications", "Id"),
            ("CaseEstimateLines", "RepairSpecificationId"),
            ("CaseRepairSpecificationSnapshots", "SpecificationId"),
        })
        {
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM {table} WHERE {column} = '{LiveId}'"));
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM {table} WHERE {column} = '{DiscardedId}'"));
        }

        foreach (var column in new[] { "DiscardedBy", "DiscardedAtUtc", "DiscardReason" })
        {
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CaseRepairSpecifications') AND name = N'{column}'"));
        }
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_CaseRepairSpecifications_Discard'"));
        // The narrowed state constraint names only the live state.
        Assert.Equal("([State]='Draft')", await database.ScalarAsync<string>(
            "SELECT definition FROM sys.check_constraints WHERE name = N'CK_CaseRepairSpecifications_State'"));
    }

    // The primary work's id is the Case's own (CaseWorksAndTriageCases).
    private const string SpecificationsSql =
        $"""
        INSERT INTO CaseRepairSpecifications
            (Id, WorkId, Version, State, SourceRoute, Name, IsCurrent, RepairerVatStatus, VatPercent,
             RegionalUplift, SupplementaryExplainOnReport, CreatedBy, CreationOperationKey, CreatedAtUtc,
             DiscardedBy, DiscardedAtUtc, DiscardReason)
        VALUES
            ('{LiveId}', '{CaseId}', 1, N'Draft', N'Manual', N'In use', 1, N'Unknown', 20,
             0, 0, N'engineer', N'spec-1', '{Recorded}',
             NULL, NULL, NULL),
            ('{DiscardedId}', '{CaseId}', 2, N'Discarded', N'Manual', N'Wrong', 0, N'Unknown', 20,
             0, 0, N'engineer', N'spec-2', '{Recorded}',
             N'engineer', '{Recorded}', N'Entered in error');

        INSERT INTO CaseEstimateLines
            (Id, WorkId, RepairSpecificationId, Position, LineType, Description, Price, Unpriced,
             RecordedByKind, RecordedBy, RecordedAtUtc)
        VALUES
            (NEWID(), '{CaseId}', '{LiveId}', 1, N'new_part', N'Front bumper', 125.00, 0,
             N'Staff', N'engineer', '{Recorded}'),
            (NEWID(), '{CaseId}', '{DiscardedId}', 1, N'new_part', N'Rear bumper', 90.00, 0,
             N'Staff', N'engineer', '{Recorded}');

        INSERT INTO CaseRepairSpecificationSnapshots
            (Id, WorkId, SpecificationId, Number, Kind, Origin, CreatedBy, CreatedAtUtc,
             DetailsJson, LinesJson, SupplementaryJson, ContentHash, Gross, SentOnReport)
        VALUES
            (NEWID(), '{CaseId}', '{LiveId}', 1, N'Imported', N'import', N'engineer', '{Recorded}',
             N'{{}}', N'[]', N'{{}}', REPLICATE(N'9', 64), 150.00, 0),
            (NEWID(), '{CaseId}', '{DiscardedId}', 1, N'Imported', N'import', N'engineer', '{Recorded}',
             N'{{}}', N'[]', N'{{}}', REPLICATE(N'8', 64), 108.00, 0);
        """;
}
