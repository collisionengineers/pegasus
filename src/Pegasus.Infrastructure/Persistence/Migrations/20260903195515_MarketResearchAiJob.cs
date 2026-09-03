using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MarketResearchAiJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiJobs_Kind",
                table: "AiJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiJobs_ResultKind",
                table: "AiJobs");

            migrationBuilder.AddColumn<string>(
                name: "MarketResearchCompletionHash",
                table: "AiJobs",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MarketResearchDocumentOccurrenceId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MarketResearchDocumentVersionId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MarketResearchMileage",
                table: "AiJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MarketResearchRecordedDate",
                table: "AiJobs",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MarketResearchRecordedTime",
                table: "AiJobs",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketResearchRetailValue",
                table: "AiJobs",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarketResearchTradeValue",
                table: "AiJobs",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MarketResearchValuationId",
                table: "AiJobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations",
                sql: "[Source] IN ('Glasses', 'Cazana', 'EngineersValue', 'AiMarketResearch')");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_MarketResearchDocumentOccurrenceId",
                table: "AiJobs",
                column: "MarketResearchDocumentOccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_MarketResearchValuationId",
                table: "AiJobs",
                column: "MarketResearchValuationId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiJobs_Kind",
                table: "AiJobs",
                sql: "[Kind] IN ('Estimate', 'UnidentifiedResolution', 'QueryResponse', 'UnidentifiedQueuePass', 'MarketResearch')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiJobs_MarketResearchResult",
                table: "AiJobs",
                sql: "([ResultKind] = 'MarketResearch' AND [MarketResearchDocumentOccurrenceId] IS NOT NULL AND [MarketResearchDocumentVersionId] IS NOT NULL AND [MarketResearchValuationId] IS NOT NULL AND [MarketResearchRecordedDate] IS NOT NULL AND [MarketResearchRecordedTime] IS NOT NULL AND [MarketResearchMileage] >= 0 AND [MarketResearchRetailValue] >= 0 AND [MarketResearchTradeValue] >= 0 AND [MarketResearchCompletionHash] IS NOT NULL) OR ([ResultKind] IS NULL OR [ResultKind] <> 'MarketResearch') AND [MarketResearchDocumentOccurrenceId] IS NULL AND [MarketResearchDocumentVersionId] IS NULL AND [MarketResearchValuationId] IS NULL AND [MarketResearchRecordedDate] IS NULL AND [MarketResearchRecordedTime] IS NULL AND [MarketResearchMileage] IS NULL AND [MarketResearchRetailValue] IS NULL AND [MarketResearchTradeValue] IS NULL AND [MarketResearchCompletionHash] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiJobs_ResultKind",
                table: "AiJobs",
                sql: "[ResultKind] IS NULL OR [ResultKind] IN ('Estimate', 'ProposedResolution', 'DraftReply', 'MarketResearch')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations");

            migrationBuilder.DropIndex(
                name: "IX_AiJobs_MarketResearchDocumentOccurrenceId",
                table: "AiJobs");

            migrationBuilder.DropIndex(
                name: "IX_AiJobs_MarketResearchValuationId",
                table: "AiJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiJobs_Kind",
                table: "AiJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiJobs_MarketResearchResult",
                table: "AiJobs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiJobs_ResultKind",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchCompletionHash",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchDocumentOccurrenceId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchDocumentVersionId",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchMileage",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchRecordedDate",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchRecordedTime",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchRetailValue",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchTradeValue",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "MarketResearchValuationId",
                table: "AiJobs");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations",
                sql: "[Source] IN ('Glasses', 'Cazana', 'EngineersValue')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiJobs_Kind",
                table: "AiJobs",
                sql: "[Kind] IN ('Estimate', 'UnidentifiedResolution', 'QueryResponse', 'UnidentifiedQueuePass')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiJobs_ResultKind",
                table: "AiJobs",
                sql: "[ResultKind] IS NULL OR [ResultKind] IN ('Estimate', 'ProposedResolution', 'DraftReply')");
        }
    }
}
