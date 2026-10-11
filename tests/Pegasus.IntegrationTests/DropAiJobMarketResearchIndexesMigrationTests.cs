using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261011130000_DropAiJobMarketResearchIndexes: nothing reads AiJobs by
/// market-research document occurrence or valuation, so both indexes go and
/// the columns stay.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DropAiJobMarketResearchIndexesMigrationTests
{
    private const string PreviousMigration = "20261011112442_DiscardDeletesRepairSpec";

    [Fact]
    public async Task MigrationDropsTheTwoMarketResearchIndexes()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        Assert.Equal(2, await MarketResearchIndexCount(database));

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(0, await MarketResearchIndexCount(database));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'AiJobs') AND name IN (N'MarketResearchDocumentOccurrenceId', N'MarketResearchValuationId')"));
    }

    private static Task<int> MarketResearchIndexCount(LocalDbTestDatabase database) =>
        database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'AiJobs') AND name IN (N'IX_AiJobs_MarketResearchDocumentOccurrenceId', N'IX_AiJobs_MarketResearchValuationId')");
}
