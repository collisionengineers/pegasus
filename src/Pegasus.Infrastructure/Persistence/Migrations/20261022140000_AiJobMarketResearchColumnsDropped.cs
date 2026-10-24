using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// An AI market research valuation carries its retail value only (R185), so
/// the five AiJobs copies of the card the completion wrote and nothing read
/// go (R313): the recorded date and time, the mileage, the retail value and
/// the trade value. The card itself stays in CaseValuations. SQL Server will
/// not drop a column a check names, so the check goes first and returns over
/// the columns that remain, the same either-all-set-or-all-null shape.
/// Destructive and forward-only: the dropped values are not recoverable.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261022140000_AiJobMarketResearchColumnsDropped")]
public class AiJobMarketResearchColumnsDropped : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropCheckConstraint(
            name: "CK_AiJobs_MarketResearchResult",
            table: "AiJobs");

        migrationBuilder.DropColumn(name: "MarketResearchMileage", table: "AiJobs");
        migrationBuilder.DropColumn(name: "MarketResearchRecordedDate", table: "AiJobs");
        migrationBuilder.DropColumn(name: "MarketResearchRecordedTime", table: "AiJobs");
        migrationBuilder.DropColumn(name: "MarketResearchRetailValue", table: "AiJobs");
        migrationBuilder.DropColumn(name: "MarketResearchTradeValue", table: "AiJobs");

        migrationBuilder.AddCheckConstraint(
            name: "CK_AiJobs_MarketResearchResult",
            table: "AiJobs",
            sql: "([ResultKind] = 'MarketResearch' AND [MarketResearchDocumentOccurrenceId] IS NOT NULL AND [MarketResearchDocumentVersionId] IS NOT NULL AND [MarketResearchValuationId] IS NOT NULL AND [MarketResearchCompletionHash] IS NOT NULL) OR (([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') AND [MarketResearchDocumentOccurrenceId] IS NULL AND [MarketResearchDocumentVersionId] IS NULL AND [MarketResearchValuationId] IS NULL AND [MarketResearchCompletionHash] IS NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => throw new NotSupportedException(
            "Dropping the AiJobs market research columns is a forward-only schema change.");
}
