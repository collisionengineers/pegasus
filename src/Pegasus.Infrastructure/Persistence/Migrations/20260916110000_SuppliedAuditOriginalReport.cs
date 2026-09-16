using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// One original report may be supplied for a waiting Audit receipt. The report
/// outcome determines the one Audit Case/PO identity, so accepting a second
/// report would make its evidence ambiguous.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260916110000_SuppliedAuditOriginalReport")]
public partial class SuppliedAuditOriginalReport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "UX_IntakeAssets_SuppliedOriginalReport",
            table: "IntakeAssets",
            column: "IntakeReceiptId",
            unique: true,
            filter: "[Disposition] = 'supplied_original_report'");

        // This is only the open generic reason produced for a classified Audit
        // whose original report was absent. Conflicts, unreadable material and
        // staff-refused work keep their recorded reasons; resolved rows remain
        // history. A linked or manually associated receipt is not waiting for a
        // new Audit Case/PO and is deliberately excluded.
        migrationBuilder.Sql("""
            UPDATE ui
            SET ui.ReasonCode = N'AuditOriginalReportMissing',
                ui.Version = ui.Version + 1
            FROM UnidentifiedItems AS ui
            INNER JOIN IntakeReceipts AS r
                ON ui.OriginKind = N'Receipt' AND ui.OriginId = r.Id
            INNER JOIN IntakeMailClassificationDecisions AS c
                ON c.IntakeReceiptId = r.Id
            WHERE ui.State = N'Open'
                AND ui.ReasonCode = N'NoUsableIdentification'
                AND r.Decision = N'needs_sorting'
                AND r.DecisionReason IN (
                    N'A standalone Audit instruction requires one attached original report stating Repairable or Total loss.',
                    N'The Audit instruction arrived without the original report it audits. Add that report to continue.')
                AND c.CaseType = N'audit'
                AND c.StandaloneAuditReportAssetSourceLabel IS NULL
                AND c.StandaloneAuditReportAssessment IS NULL
                AND NOT EXISTS (
                    SELECT 1 FROM StandaloneAuditEvidence AS e
                    WHERE e.IntakeReceiptId = r.Id)
                AND NOT EXISTS (
                    SELECT 1 FROM CaseIntakeLinks AS l
                    WHERE l.IntakeReceiptId = r.Id)
                AND NOT EXISTS (
                    SELECT 1 FROM IntakeManualAssociations AS a
                    WHERE a.IntakeReceiptId = r.Id AND a.IsActive = 1)
                AND NOT EXISTS (
                    SELECT 1 FROM IntakeAssets AS asset
                    WHERE asset.IntakeReceiptId = r.Id
                        AND asset.Disposition = N'supplied_original_report');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_IntakeAssets_SuppliedOriginalReport",
            table: "IntakeAssets");
    }
}
