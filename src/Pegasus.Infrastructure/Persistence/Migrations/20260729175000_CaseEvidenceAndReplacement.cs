using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729175000_CaseEvidenceAndReplacement")]
public partial class CaseEvidenceAndReplacement : Migration
{
    private static readonly string[] MailboxItemColumns = ["MailboxIdentity", "ImmutableItemIdentity"];
    private static readonly string[] CaseItemColumns = ["CaseId", "ImmutableItemIdentity"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Cases_OriginIntakeReceiptId",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_Cases_StandaloneAuditEvidenceId",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_CaseId_ImmutableItemIdentity",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflows_ReplacementCaseId",
            table: "CaseWorkflows");

        migrationBuilder.AddColumn<Guid>(
            name: "OriginalCaseId",
            table: "CaseWorkflows",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DiscoveredAtUtc",
            table: "CaseReportSentEvidence",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DiscoveredByKind",
            table: "CaseReportSentEvidence",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DiscoveredBySubjectId",
            table: "CaseReportSentEvidence",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RetentionOperationKey",
            table: "CaseReportSentEvidence",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RetentionRequestHash",
            table: "CaseReportSentEvidence",
            fixedLength: true,
            maxLength: 64,
            nullable: true);

        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }

        migrationBuilder.Sql(
            """
            UPDATE [CaseReportSentEvidence]
            SET [DiscoveredAtUtc] = [LinkedAtUtc],
                [DiscoveredByKind] = 'LegacyUnverified',
                [DiscoveredBySubjectId] = 'legacy-migration',
                [RetentionOperationKey] = 'legacy:' + REPLACE(CONVERT(nvarchar(36), [Id]), '-', ''),
                [RetentionRequestHash] = REPLICATE('0', 64);
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "CaseId",
            table: "CaseReportSentEvidence",
            nullable: true,
            oldClrType: typeof(Guid));

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "DiscoveredAtUtc",
            table: "CaseReportSentEvidence",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "DiscoveredByKind",
            table: "CaseReportSentEvidence",
            maxLength: 40,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 40,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "DiscoveredBySubjectId",
            table: "CaseReportSentEvidence",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "LinkedAtUtc",
            table: "CaseReportSentEvidence",
            nullable: true,
            oldClrType: typeof(DateTimeOffset));

        migrationBuilder.AlterColumn<string>(
            name: "LinkedByKind",
            table: "CaseReportSentEvidence",
            maxLength: 40,
            nullable: true,
            oldClrType: typeof(string),
            oldMaxLength: 40);

        migrationBuilder.AlterColumn<string>(
            name: "LinkedByRolesJson",
            table: "CaseReportSentEvidence",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldMaxLength: 500);

        migrationBuilder.AlterColumn<string>(
            name: "LinkedBySubjectId",
            table: "CaseReportSentEvidence",
            maxLength: 200,
            nullable: true,
            oldClrType: typeof(string),
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<string>(
            name: "RetentionOperationKey",
            table: "CaseReportSentEvidence",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "RetentionRequestHash",
            table: "CaseReportSentEvidence",
            fixedLength: true,
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Cases_OriginIntakeReceiptId",
            table: "Cases",
            column: "OriginIntakeReceiptId");

        migrationBuilder.CreateIndex(
            name: "IX_Cases_StandaloneAuditEvidenceId",
            table: "Cases",
            column: "StandaloneAuditEvidenceId");

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_CaseId",
            table: "CaseReportSentEvidence",
            column: "CaseId");

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
            table: "CaseReportSentEvidence",
            columns: MailboxItemColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_RetentionOperationKey",
            table: "CaseReportSentEvidence",
            column: "RetentionOperationKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflows_OriginalCaseId",
            table: "CaseWorkflows",
            column: "OriginalCaseId",
            unique: true,
            filter: "[OriginalCaseId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflows_ReplacementCaseId",
            table: "CaseWorkflows",
            column: "ReplacementCaseId",
            unique: true,
            filter: "[ReplacementCaseId] IS NOT NULL");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseWorkflows_OriginalNotSelf",
            table: "CaseWorkflows",
            sql: "[OriginalCaseId] IS NULL OR [OriginalCaseId] <> [CaseId]");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseWorkflows_ReplacementNotSelf",
            table: "CaseWorkflows",
            sql: "[ReplacementCaseId] IS NULL OR [ReplacementCaseId] <> [CaseId]");

        migrationBuilder.AddForeignKey(
            name: "FK_CaseWorkflows_Cases_OriginalCaseId",
            table: "CaseWorkflows",
            column: "OriginalCaseId",
            principalTable: "Cases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }

        migrationBuilder.Sql(
            """
            IF EXISTS (SELECT 1 FROM [CaseWorkflows] WHERE [OriginalCaseId] IS NOT NULL)
               OR EXISTS (SELECT 1 FROM [CaseReportSentEvidence] WHERE [CaseId] IS NULL)
                THROW 51000, 'Cannot remove retained evidence or immutable replacement-link schema while dependent records exist.', 1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_CaseWorkflows_Cases_OriginalCaseId",
            table: "CaseWorkflows");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseWorkflows_OriginalNotSelf",
            table: "CaseWorkflows");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseWorkflows_ReplacementNotSelf",
            table: "CaseWorkflows");

        migrationBuilder.DropIndex(
            name: "IX_Cases_OriginIntakeReceiptId",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_Cases_StandaloneAuditEvidenceId",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_CaseId",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_MailboxIdentity_ImmutableItemIdentity",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropIndex(
            name: "IX_CaseReportSentEvidence_RetentionOperationKey",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflows_OriginalCaseId",
            table: "CaseWorkflows");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflows_ReplacementCaseId",
            table: "CaseWorkflows");

        migrationBuilder.AlterColumn<Guid>(
            name: "CaseId",
            table: "CaseReportSentEvidence",
            nullable: false,
            oldClrType: typeof(Guid),
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTimeOffset>(
            name: "LinkedAtUtc",
            table: "CaseReportSentEvidence",
            nullable: false,
            oldClrType: typeof(DateTimeOffset),
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LinkedByKind",
            table: "CaseReportSentEvidence",
            maxLength: 40,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 40,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LinkedByRolesJson",
            table: "CaseReportSentEvidence",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "LinkedBySubjectId",
            table: "CaseReportSentEvidence",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldMaxLength: 200,
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "OriginalCaseId",
            table: "CaseWorkflows");

        migrationBuilder.DropColumn(
            name: "DiscoveredAtUtc",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropColumn(
            name: "DiscoveredByKind",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropColumn(
            name: "DiscoveredBySubjectId",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropColumn(
            name: "RetentionOperationKey",
            table: "CaseReportSentEvidence");

        migrationBuilder.DropColumn(
            name: "RetentionRequestHash",
            table: "CaseReportSentEvidence");

        migrationBuilder.CreateIndex(
            name: "IX_Cases_OriginIntakeReceiptId",
            table: "Cases",
            column: "OriginIntakeReceiptId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Cases_StandaloneAuditEvidenceId",
            table: "Cases",
            column: "StandaloneAuditEvidenceId",
            unique: true,
            filter: "[StandaloneAuditEvidenceId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportSentEvidence_CaseId_ImmutableItemIdentity",
            table: "CaseReportSentEvidence",
            columns: CaseItemColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflows_ReplacementCaseId",
            table: "CaseWorkflows",
            column: "ReplacementCaseId");
    }
}
