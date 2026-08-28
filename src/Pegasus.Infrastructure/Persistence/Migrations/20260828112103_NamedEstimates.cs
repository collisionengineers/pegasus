using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NamedEstimates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseRepairSpecifications_CaseId",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_Acceptance",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_SourceRoute",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_State",
                table: "CaseRepairSpecifications");

            migrationBuilder.AddColumn<Guid>(
                name: "AiJobId",
                table: "CaseRepairSpecifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscardReason",
                table: "CaseRepairSpecifications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DiscardedAtUtc",
                table: "CaseRepairSpecifications",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscardedBy",
                table: "CaseRepairSpecifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LabourRate",
                table: "CaseRepairSpecifications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastOperationKey",
                table: "CaseRepairSpecifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "CaseRepairSpecifications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CaseRepairSpecifications",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherCosts",
                table: "CaseRepairSpecifications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaintLabourRate",
                table: "CaseRepairSpecifications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaintMaterials",
                table: "CaseRepairSpecifications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepairDays",
                table: "CaseRepairSpecifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatPercent",
                table: "CaseRepairSpecifications",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaintWorkUnits",
                table: "CaseEstimateLines",
                type: "decimal(9,1)",
                precision: 9,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "CaseEstimateLines",
                type: "int",
                nullable: true);

            // ENG-026: every existing specification becomes a named estimate.
            // The previous filtered unique index guaranteed at most one
            // Accepted row per case, so marking it Current cannot collide
            // with the new [IsCurrent] = 1 index created below. The VAT
            // percentage backfills to the standard rate the built-in report
            // rule applied; no new table, so no runtime grant is needed.
            migrationBuilder.Sql(
                """
                UPDATE [CaseRepairSpecifications]
                SET [Name] = CONCAT('Estimate ', [Version]),
                    [VatPercent] = 20,
                    [IsCurrent] = CASE WHEN [State] = 'Accepted' THEN 1 ELSE 0 END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_AiJobId",
                table: "CaseRepairSpecifications",
                column: "AiJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_CaseId",
                table: "CaseRepairSpecifications",
                column: "CaseId",
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_Acceptance",
                table: "CaseRepairSpecifications",
                sql: "([State] IN ('Accepted', 'Superseded') AND [AcceptedBy] IS NOT NULL AND [AcceptedAtUtc] IS NOT NULL) OR ([State] = 'Draft' AND [AcceptedBy] IS NULL AND [AcceptedAtUtc] IS NULL) OR ([State] = 'Discarded' AND [DiscardedBy] IS NOT NULL AND [DiscardedAtUtc] IS NOT NULL AND [DiscardReason] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_Current",
                table: "CaseRepairSpecifications",
                sql: "[IsCurrent] = 0 OR [State] = 'Accepted'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_SourceRoute",
                table: "CaseRepairSpecifications",
                sql: "[SourceRoute] IN ('LegacyUnresolved', 'Manual', 'Glasses', 'AudatexPdf', 'ApprovedAiProposal', 'Json', 'AiDraft')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_State",
                table: "CaseRepairSpecifications",
                sql: "[State] IN ('Draft', 'Accepted', 'Superseded', 'Discarded')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_VatPercent",
                table: "CaseRepairSpecifications",
                sql: "[VatPercent] BETWEEN 0 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseEstimateLines_Quantity",
                table: "CaseEstimateLines",
                sql: "[Quantity] IS NULL OR [Quantity] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseRepairSpecifications_AiJobId",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropIndex(
                name: "IX_CaseRepairSpecifications_CaseId",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_Acceptance",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_Current",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_SourceRoute",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_State",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_VatPercent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseEstimateLines_Quantity",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "AiJobId",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "DiscardReason",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "DiscardedAtUtc",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "DiscardedBy",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "LabourRate",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "LastOperationKey",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "OtherCosts",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "PaintLabourRate",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "PaintMaterials",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "RepairDays",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "VatPercent",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "PaintWorkUnits",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "CaseEstimateLines");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_CaseId",
                table: "CaseRepairSpecifications",
                column: "CaseId",
                unique: true,
                filter: "[State] = 'Accepted'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_Acceptance",
                table: "CaseRepairSpecifications",
                sql: "([State] IN ('Accepted', 'Superseded') AND [AcceptedBy] IS NOT NULL AND [AcceptedAtUtc] IS NOT NULL) OR ([State] = 'Draft' AND [AcceptedBy] IS NULL AND [AcceptedAtUtc] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_SourceRoute",
                table: "CaseRepairSpecifications",
                sql: "[SourceRoute] IN ('LegacyUnresolved', 'Manual', 'Glasses', 'AudatexPdf', 'ApprovedAiProposal')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_State",
                table: "CaseRepairSpecifications",
                sql: "[State] IN ('Draft', 'Accepted', 'Superseded')");
        }
    }
}
