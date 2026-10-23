using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261019120000_RepairerVatStatusDefaultsToRegistered: a live repair spec
/// stored as Unknown becomes Registered. Category flags and snapshots stay.
/// 20261023150000_RepairerVatStatusUnknownRemoved: frozen versions stored as
/// Unknown become Registered too, hand-picked categories kept (R386).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RepairerVatStatusDefaultMigrationTests
{
    private const string PreviousMigration = "20261018080000_GrantWorkerContactPrincipalLinksRead";
    private const string Migration = "20261019120000_RepairerVatStatusDefaultsToRegistered";
    private const string BeforeRemoval = "20261022120000_DocumentTypesEstimateAndValuationEvidence";
    private const string Removal = "20261023150000_RepairerVatStatusUnknownRemoved";
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
            $"SELECT COUNT(*) FROM CaseRepairSpecificationSnapshots WHERE SpecificationId = '{CurrentId}' AND DetailsJson LIKE N'%\"RepairerStatus\":0,%'"));
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

    [Fact]
    public async Task FrozenUnknownVersionsBecomeRegisteredKeepingHandPickedCategories()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await context.Database.MigrateAsync(BeforeRemoval);
        await database.ExecuteAsync(RemovalSql);

        await context.Database.MigrateAsync(Removal);

        Assert.Equal(Removal, (await context.Database.GetAppliedMigrationsAsync()).Last());
        await AssertFrozenAsync(database, FrozenPlainId, EstimateVatPolicy.For(RepairerVatStatus.Registered));
        await AssertFrozenAsync(
            database, FrozenHandPickedId,
            new EstimateVatPolicy(RepairerVatStatus.Registered, EstimateVatCategories.Parts, true));
        await AssertFrozenAsync(database, FrozenAllPickedId, EstimateVatPolicy.For(RepairerVatStatus.Registered));
        Assert.Equal(
            FrozenBeforeRemoval(EstimateVatPolicy.For(RepairerVatStatus.NotRegistered)),
            await DetailsJsonAsync(database, FrozenNotRegisteredId));
    }

    [Fact]
    public async Task RemovalCannotBeReverted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(Removal);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(BeforeRemoval));

        Assert.StartsWith(
            "RepairerVatStatusUnknownRemoved is forward-only",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(Removal, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    /// <summary>The frozen version reads exactly as Freeze writes the same details on the policy given.</summary>
    private static async Task AssertFrozenAsync(LocalDbTestDatabase database, string snapshotId, EstimateVatPolicy expected)
    {
        var frozen = JsonNode.Parse(await DetailsJsonAsync(database, snapshotId));
        var written = JsonSerializer.SerializeToNode(FrozenDetails(expected), Json);
        Assert.True(JsonNode.DeepEquals(written, frozen), frozen?.ToJsonString());
    }

    private static Task<string> DetailsJsonAsync(LocalDbTestDatabase database, string snapshotId) =>
        database.ScalarAsync<string>(
            $"SELECT DetailsJson FROM CaseRepairSpecificationSnapshots WHERE Id = '{snapshotId}'");

    private static Task<string> StatusAsync(LocalDbTestDatabase database, string id) =>
        database.ScalarAsync<string>(
            $"SELECT RepairerVatStatus FROM CaseRepairSpecifications WHERE Id = '{id}'");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General);

    // The snapshot as Freeze writes it: a numeric status under the General defaults.
    private static readonly string FrozenUnknownDetails = JsonSerializer.Serialize(
        new EstimateDetails("Untouched", null, null, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Unknown)),
        Json);

    private const string FrozenPlainId = "d1000000-0000-0000-0000-000000000001";
    private const string FrozenHandPickedId = "d1000000-0000-0000-0000-000000000002";
    private const string FrozenAllPickedId = "d1000000-0000-0000-0000-000000000003";
    private const string FrozenNotRegisteredId = "d1000000-0000-0000-0000-000000000004";

    private static EstimateDetails FrozenDetails(EstimateVatPolicy vat) => new("Untouched", 80m, null, 20m, Vat: vat);

    /// <summary>
    /// A version as Freeze wrote it before the removal: the policy's computed
    /// TreatmentPending was serialised beside the status.
    /// </summary>
    private static string FrozenBeforeRemoval(EstimateVatPolicy vat)
    {
        var details = JsonSerializer.SerializeToNode(FrozenDetails(vat), Json)!;
        var pending = vat.RepairerStatus == RepairerVatStatus.Unknown && !vat.CategoriesOverridden;
        details["Vat"]!["TreatmentPending"] = pending;
        details["VatPolicy"]!["TreatmentPending"] = pending;
        return details.ToJsonString(Json);
    }

    private static readonly string RemovalSql =
        $$"""
        {{SpecificationRowsSql}}

        INSERT INTO CaseRepairSpecificationSnapshots
            (Id, WorkId, SpecificationId, Number, Kind, Origin, CreatedBy, CreatedAtUtc,
             DetailsJson, LinesJson, SupplementaryJson, ContentHash, Gross, SentOnReport)
        VALUES
            ('{{FrozenPlainId}}', '{{CaseId}}', '{{CurrentId}}', 1, N'Imported', N'import', N'engineer', '{{Recorded}}',
             N'{{FrozenBeforeRemoval(EstimateVatPolicy.For(RepairerVatStatus.Unknown))}}', N'[]', N'{}', REPLICATE(N'c', 64), 0, 0),
            ('{{FrozenHandPickedId}}', '{{CaseId}}', '{{CurrentId}}', 2, N'Imported', N'import', N'engineer', '{{Recorded}}',
             N'{{FrozenBeforeRemoval(new(RepairerVatStatus.Unknown, EstimateVatCategories.Parts, true))}}', N'[]', N'{}', REPLICATE(N'c', 64), 0, 0),
            ('{{FrozenAllPickedId}}', '{{CaseId}}', '{{CurrentId}}', 3, N'Imported', N'import', N'engineer', '{{Recorded}}',
             N'{{FrozenBeforeRemoval(new(RepairerVatStatus.Unknown, EstimateVatCategories.All, true))}}', N'[]', N'{}', REPLICATE(N'c', 64), 0, 0),
            ('{{FrozenNotRegisteredId}}', '{{CaseId}}', '{{CurrentId}}', 4, N'Imported', N'import', N'engineer', '{{Recorded}}',
             N'{{FrozenBeforeRemoval(EstimateVatPolicy.For(RepairerVatStatus.NotRegistered))}}', N'[]', N'{}', REPLICATE(N'c', 64), 0, 0);
        """;

    private const string SpecificationRowsSql =
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
        """;

    private static readonly string SpecificationsSql =
        $$"""
        {{SpecificationRowsSql}}

        INSERT INTO CaseRepairSpecificationSnapshots
            (Id, WorkId, SpecificationId, Number, Kind, Origin, CreatedBy, CreatedAtUtc,
             DetailsJson, LinesJson, SupplementaryJson, ContentHash, Gross, SentOnReport)
        VALUES
            (NEWID(), '{{CaseId}}', '{{CurrentId}}', 1, N'Imported', N'import', N'engineer', '{{Recorded}}',
             N'{{FrozenUnknownDetails}}', N'[]', N'{}', REPLICATE(N'c', 64), 0, 0);
        """;
}
