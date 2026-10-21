using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261019120000_RepairerVatStatusDefaultsToRegistered: a live repair spec
/// stored as Unknown becomes Registered. Category flags and snapshots stay.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RepairerVatStatusDefaultMigrationTests
{
    private const string PreviousMigration = "20261018080000_GrantWorkerContactPrincipalLinksRead";
    private const string Migration = "20261019120000_RepairerVatStatusDefaultsToRegistered";
    private const string CaseId = RepairSpecInUseOnCreateMigrationTests.CaseId;
    private const string CurrentId = "c1000000-0000-0000-0000-000000000001";
    private const string HandPickedId = "c1000000-0000-0000-0000-000000000002";
    private const string RegisteredId = "c1000000-0000-0000-0000-000000000003";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    [Fact]
    public async Task LiveUnknownStatusesBecomeRegisteredAndSnapshotsStay()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SpecificationsSql);

        await context.Database.MigrateAsync(Migration);

        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal("Registered", await StatusAsync(database, CurrentId));
        Assert.Equal("Registered", await StatusAsync(database, HandPickedId));
        Assert.Equal("Registered", await StatusAsync(database, RegisteredId));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT CONVERT(int, LabourVatApplicable) FROM CaseRepairSpecifications WHERE Id = '{HandPickedId}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT CONVERT(int, PartsVatApplicable) FROM CaseRepairSpecifications WHERE Id = '{HandPickedId}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseRepairSpecifications WHERE Id = '{CurrentId}' AND LabourVatApplicable IS NOT NULL"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseRepairSpecificationSnapshots WHERE SpecificationId = '{CurrentId}' AND DetailsJson LIKE N'%Unknown%'"));
    }

    [Fact]
    public async Task MigrationCannotBeReverted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(Migration);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(PreviousMigration));

        Assert.StartsWith(
            "RepairerVatStatusDefaultsToRegistered is forward-only",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    private static Task<string> StatusAsync(LocalDbTestDatabase database, string id) =>
        database.ScalarAsync<string>(
            $"SELECT RepairerVatStatus FROM CaseRepairSpecifications WHERE Id = '{id}'");

    private const string SpecificationsSql =
        $$"""
        INSERT INTO CaseRepairSpecifications
            (Id, WorkId, Version, State, SourceRoute, Name, IsCurrent, RepairerVatStatus, VatPercent,
             RegionalUplift, SupplementaryExplainOnReport, CreatedBy, CreationOperationKey, CreatedAtUtc,
             LabourVatApplicable, PartsVatApplicable, MaterialsVatApplicable, SpecialistVatApplicable)
        VALUES
            ('{{CurrentId}}', '{{CaseId}}', 1, N'Draft', N'Manual', N'Untouched', 1, N'Unknown', 20,
             0, 0, N'engineer', N'vat-default-1', '{{Recorded}}',
             NULL, NULL, NULL, NULL),
            ('{{HandPickedId}}', '{{CaseId}}', 2, N'Draft', N'Manual', N'Hand-picked', 0, N'Unknown', 20,
             0, 0, N'engineer', N'vat-default-2', '{{Recorded}}',
             1, 0, 0, 0),
            ('{{RegisteredId}}', '{{CaseId}}', 3, N'Draft', N'Manual', N'Chosen', 0, N'Registered', 20,
             0, 0, N'engineer', N'vat-default-3', '{{Recorded}}',
             NULL, NULL, NULL, NULL);

        INSERT INTO CaseRepairSpecificationSnapshots
            (Id, WorkId, SpecificationId, Number, Kind, Origin, CreatedBy, CreatedAtUtc,
             DetailsJson, LinesJson, SupplementaryJson, ContentHash, Gross, SentOnReport)
        VALUES
            (NEWID(), '{{CaseId}}', '{{CurrentId}}', 1, N'Imported', N'import', N'engineer', '{{Recorded}}',
             N'{"repairerVatStatus":"Unknown"}', N'[]', N'{}', REPLICATE(N'c', 64), 0, 0);
        """;
}
