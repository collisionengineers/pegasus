using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnidentifiedAuditOriginalReportMissing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_IntakeAssets_IntakeReceiptId",
                table: "IntakeAssets",
                column: "IntakeReceiptId",
                unique: true,
                filter: "[Disposition] = 'supplied_original_report'");

            // The new AuditOriginalReportMissing reason restates what an open,
            // report-less Audit Unidentified item already is (operator decision
            // 2026-09-11): the Audit instruction arrived without the original
            // report it audits, so the item waits instead of allocating. Open
            // items whose origin receipt is a classified Audit still at
            // Needs sorting with no original-report evidence take the reason
            // now, so the live queue shows the right reason and next step
            // before any later pass re-registers them. Resolved items keep
            // their recorded reason: it is history, not current state.
            migrationBuilder.Sql("""
                UPDATE ui
                SET ui.ReasonCode = N'AuditOriginalReportMissing'
                FROM UnidentifiedItems AS ui
                INNER JOIN IntakeReceipts AS r
                    ON ui.OriginKind = N'Receipt' AND ui.OriginId = r.Id
                INNER JOIN IntakeMailClassificationDecisions AS c
                    ON c.IntakeReceiptId = r.Id
                WHERE ui.State = N'Open'
                    AND ui.ReasonCode <> N'AuditOriginalReportMissing'
                    AND r.Decision = N'needs_sorting'
                    AND c.CaseType = N'Audit'
                    AND c.StandaloneAuditReportAssetSourceLabel IS NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM StandaloneAuditEvidence AS e
                        WHERE e.IntakeReceiptId = r.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntakeAssets_IntakeReceiptId",
                table: "IntakeAssets");
        }
    }
}
