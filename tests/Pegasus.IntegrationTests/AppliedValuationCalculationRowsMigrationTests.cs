using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261023160000_AppliedValuationCalculationRows (register A4-03, R352): the
/// calculation and additions of every applied valuation already recorded are
/// filled from the snapshot JSON, in the order the JSON holds them, and the
/// snapshot JSON is left as it was.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AppliedValuationCalculationRowsMigrationTests
{
    private const string PreviousMigration = "20261023120000_GlassRepairEstimateSessionCallbackDigestIndex";
    private const string Migration = "20261023160000_AppliedValuationCalculationRows";
    private const string CaseId = RepairSpecInUseOnCreateMigrationTests.CaseId;
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);
    private static readonly Guid GuideId =Guid.Parse("a7000000-0000-0000-0000-000000000001");
    private static readonly Guid TowBarId = Guid.Parse("a7000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset GuideStamp = new(2031, 5, 6, 10, 30, 0, 123, TimeSpan.Zero);

    private static readonly EfValuationStore.AppliedValuationSnapshot WithAdditions = new(
        7,
        GuideId,
        GuideStamp,
        new ValuationCalculation(
            GuideRetailValue: 3100m,
            CommercialVatApplied: true,
            CommercialVatAmount: 620m,
            ValueIncludingVat: 3720m,
            PriorTotalLossPercentage: 0.20m,
            PriorTotalLossAmount: 744m,
            Additions:
            [
                new(TowBarId, 2, "Tow bar", 350m, 300m),
                new(Guid.Empty, 0, "Roof rack", null, 125.50m)
            ],
            AdditionsTotal: 425.50m,
            ConditionDeduction: 25m,
            Proposal: 3377m));

    private static readonly EfValuationStore.AppliedValuationSnapshot WithoutAdditions = new(
        8,
        GuideId,
        GuideStamp,
        new ValuationCalculation(
            GuideRetailValue: 3100m,
            CommercialVatApplied: false,
            CommercialVatAmount: 0m,
            ValueIncludingVat: 3100m,
            PriorTotalLossPercentage: null,
            PriorTotalLossAmount: 0m,
            Additions: [],
            AdditionsTotal: 0m,
            ConditionDeduction: 0m,
            Proposal: 3100m));

    [Fact]
    public async Task MigrationFillsTheCalculationRowsFromEverySnapshotAndLeavesTheJson()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        var withAdditions = Guid.NewGuid();
        var withoutAdditions = Guid.NewGuid();
        await database.ExecuteAsync(SnapshotSql(withAdditions, WithAdditions, 'c')
            + SnapshotSql(withoutAdditions, WithoutAdditions, 'd'));

        await context.Database.MigrateAsync(Migration);

        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(2, await database.ScalarAsync<int>("SELECT COUNT(*) FROM AppliedValuationCalculations"));
        Assert.Equal(2, await database.ScalarAsync<int>("SELECT COUNT(*) FROM AppliedValuationAdditions"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM AppliedValuationSnapshots WHERE SnapshotJson LIKE N'%\"calculation\"%'"));

        var filled = await context.Set<AppliedValuationCalculationEntity>().AsNoTracking()
            .SingleAsync(item => item.SnapshotId == withAdditions);
        Assert.Equal(
            (7L, GuideId, GuideStamp, 3100m, true, 620m, 3720m, (decimal?)0.20m, 744m, 425.50m, 25m, 3377m),
            (filled.CaseVersion, filled.GuideValuationId, filled.GuideValuationStampUtc,
                filled.GuideRetailValue, filled.CommercialVatApplied, filled.CommercialVatAmount,
                filled.ValueIncludingVat, filled.PriorTotalLossPercentage, filled.PriorTotalLossAmount,
                filled.AdditionsTotal, filled.ConditionDeduction, filled.Proposal));
        var additions = await context.Set<AppliedValuationAdditionEntity>().AsNoTracking()
            .Where(item => item.SnapshotId == withAdditions)
            .OrderBy(item => item.Ordinal)
            .ToListAsync();
        Assert.Equal(
            [
                (0, TowBarId, 2L, "Tow bar", (decimal?)350m, 300m),
                (1, Guid.Empty, 0L, "Roof rack", (decimal?)null, 125.50m)
            ],
            additions.Select(item => (item.Ordinal, item.PresetId, item.PresetVersion, item.Label, item.SuggestedAmount, item.Amount)));

        var bare = await context.Set<AppliedValuationCalculationEntity>().AsNoTracking()
            .SingleAsync(item => item.SnapshotId == withoutAdditions);
        Assert.Null(bare.PriorTotalLossPercentage);
        Assert.False(bare.CommercialVatApplied);
        Assert.Equal(3100m, bare.Proposal);
        Assert.Empty(await context.Set<AppliedValuationAdditionEntity>().AsNoTracking()
            .Where(item => item.SnapshotId == withoutAdditions).ToListAsync());
    }

    [Fact]
    public async Task MigrationStopsOnASnapshotTheJsonCannotFill()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SnapshotSql(Guid.NewGuid(), WithoutAdditions, 'e').Replace(
            "\"proposal\"",
            "\"proposalMissing\"",
            StringComparison.Ordinal));

        await Assert.ThrowsAnyAsync<Exception>(() => context.Database.MigrateAsync(Migration));
        Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    private static string SnapshotSql(Guid id, EfValuationStore.AppliedValuationSnapshot snapshot, char hash) =>
        $$"""
        INSERT INTO AppliedValuationSnapshots
            (Id, WorkId, SnapshotJson, CalculationPolicyVersion, GeneratedByKind, GeneratedBySubjectId,
             SnapshotHash, AcceptedEngineerValue, AcceptedBy, AcceptedAtUtc, Reason, PolicyVersion)
        VALUES
            ('{{id}}', '{{CaseId}}', N'{{JsonSerializer.Serialize(snapshot, SnapshotJson)}}',
             N'1', N'Staff', N'migration-test', REPLICATE(N'{{hash}}', 64), {{snapshot.Calculation.Proposal}},
             N'migration-test', '{{Recorded}}', N'Migration test', N'1');

        """;
}
