using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729173000_AuditIdentityEvidence")]
public partial class AuditIdentityEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: StandaloneAuditEvidence - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseEngineerFindings - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.AddColumn<DateOnly>(
            name: "InspectionDate",
            table: "InstructionDrafts",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "AcceptedInspectionDeadline",
            table: "Cases",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AuditCustodyConfirmedAtUtc",
            table: "Cases",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AuditCustodyRemoteId",
            table: "Cases",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "StandaloneAuditEvidenceId",
            table: "Cases",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "StandaloneAuditEvidence",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                IntakeReceiptId = table.Column<Guid>(nullable: false),
                OriginalReportAssetId = table.Column<Guid>(nullable: false),
                Assessment = table.Column<string>(maxLength: 40, nullable: false),
                ConfirmedByKind = table.Column<string>(maxLength: 40, nullable: false),
                ConfirmedBySubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ConfirmedByRolesJson = table.Column<string>(nullable: false),
                ConfirmedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                RequestHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                ResultingReceiptVersion = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StandaloneAuditEvidence", x => x.Id);
                table.ForeignKey(
                    name: "FK_StandaloneAuditEvidence_IntakeAssets_OriginalReportAssetId",
                    column: x => x.OriginalReportAssetId,
                    principalTable: "IntakeAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StandaloneAuditEvidence_IntakeReceipts_IntakeReceiptId",
                    column: x => x.IntakeReceiptId,
                    principalTable: "IntakeReceipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CaseEngineerFindings",
            columns: table => new
            {
                CaseId = table.Column<Guid>(nullable: false),
                Assessment = table.Column<string>(maxLength: 40, nullable: false),
                RecordedByKind = table.Column<string>(maxLength: 40, nullable: false),
                RecordedBySubjectId = table.Column<string>(maxLength: 200, nullable: false),
                RecordedByRolesJson = table.Column<string>(nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                RecordedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestHash = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                CustodyWorkId = table.Column<Guid>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseEngineerFindings", x => x.CaseId);
                table.ForeignKey(
                    name: "FK_CaseEngineerFindings_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CaseEngineerFindings_ExternalWorkItems_CustodyWorkId",
                    column: x => x.CustodyWorkId,
                    principalTable: "ExternalWorkItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Cases_StandaloneAuditEvidenceId",
            table: "Cases",
            column: "StandaloneAuditEvidenceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StandaloneAuditEvidence_IntakeReceiptId",
            table: "StandaloneAuditEvidence",
            column: "IntakeReceiptId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StandaloneAuditEvidence_OperationKey",
            table: "StandaloneAuditEvidence",
            column: "OperationKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StandaloneAuditEvidence_OriginalReportAssetId",
            table: "StandaloneAuditEvidence",
            column: "OriginalReportAssetId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CaseEngineerFindings_CustodyWorkId",
            table: "CaseEngineerFindings",
            column: "CustodyWorkId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CaseEngineerFindings_OperationKey",
            table: "CaseEngineerFindings",
            column: "OperationKey",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_Cases_StandaloneAuditEvidence_StandaloneAuditEvidenceId",
            table: "Cases",
            column: "StandaloneAuditEvidenceId",
            principalTable: "StandaloneAuditEvidence",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CaseEngineerFindings");

        migrationBuilder.DropForeignKey(
            name: "FK_Cases_StandaloneAuditEvidence_StandaloneAuditEvidenceId",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_Cases_StandaloneAuditEvidenceId",
            table: "Cases");

        migrationBuilder.DropTable(name: "StandaloneAuditEvidence");

        migrationBuilder.DropColumn(
            name: "AcceptedInspectionDeadline",
            table: "Cases");

        migrationBuilder.DropColumn(
            name: "AuditCustodyConfirmedAtUtc",
            table: "Cases");

        migrationBuilder.DropColumn(
            name: "AuditCustodyRemoteId",
            table: "Cases");

        migrationBuilder.DropColumn(
            name: "StandaloneAuditEvidenceId",
            table: "Cases");

        migrationBuilder.DropColumn(
            name: "InspectionDate",
            table: "InstructionDrafts");
    }
}
