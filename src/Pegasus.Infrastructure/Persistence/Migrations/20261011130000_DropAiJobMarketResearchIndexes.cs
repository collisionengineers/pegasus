using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Nothing reads AiJobs by market-research document occurrence or valuation:
/// the columns are written on completion and read back in memory. The two
/// indexes only cost writes, so they go. Forward-only: no data change.
/// </summary>
public partial class DropAiJobMarketResearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AiJobs_MarketResearchDocumentOccurrenceId",
            table: "AiJobs");

        migrationBuilder.DropIndex(
            name: "IX_AiJobs_MarketResearchValuationId",
            table: "AiJobs");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_AiJobs_MarketResearchDocumentOccurrenceId",
            table: "AiJobs",
            column: "MarketResearchDocumentOccurrenceId");

        migrationBuilder.CreateIndex(
            name: "IX_AiJobs_MarketResearchValuationId",
            table: "AiJobs",
            column: "MarketResearchValuationId");
    }
}
