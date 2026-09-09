using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909091500_RemoveCaseDocumentOcrOperations")]
public partial class RemoveCaseDocumentOcrOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }

        migrationBuilder.Sql(
            """
            IF EXISTS (
                SELECT 1
                FROM dbo.IntakeOcrOperations
                WHERE DocumentVersionId IS NOT NULL OR IntakeAssetId IS NULL)
                THROW 51000, N'Case-document OCR operations must be removed before this migration.', 1;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_IntakeOcrOperations_DocumentVersions_DocumentVersionId",
            table: "IntakeOcrOperations");

        migrationBuilder.DropIndex(
            name: "IX_IntakeOcrOperations_DocumentVersionId_SourceSha256",
            table: "IntakeOcrOperations");

        migrationBuilder.DropCheckConstraint(
            name: "CK_IntakeOcrOperations_Source",
            table: "IntakeOcrOperations");

        migrationBuilder.DropColumn(
            name: "DocumentVersionId",
            table: "IntakeOcrOperations");

        migrationBuilder.DropIndex(
            name: "IX_IntakeOcrOperations_IntakeAssetId",
            table: "IntakeOcrOperations");

        migrationBuilder.AlterColumn<Guid>(
            name: "IntakeAssetId",
            table: "IntakeOcrOperations",
            type: "uniqueidentifier",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_IntakeOcrOperations_IntakeAssetId",
            table: "IntakeOcrOperations",
            column: "IntakeAssetId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }

        migrationBuilder.DropIndex(
            name: "IX_IntakeOcrOperations_IntakeAssetId",
            table: "IntakeOcrOperations");

        migrationBuilder.AlterColumn<Guid>(
            name: "IntakeAssetId",
            table: "IntakeOcrOperations",
            type: "uniqueidentifier",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier");

        migrationBuilder.CreateIndex(
            name: "IX_IntakeOcrOperations_IntakeAssetId",
            table: "IntakeOcrOperations",
            column: "IntakeAssetId");

        migrationBuilder.AddColumn<Guid>(
            name: "DocumentVersionId",
            table: "IntakeOcrOperations",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_IntakeOcrOperations_Source",
            table: "IntakeOcrOperations",
            sql: "([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL) OR ([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_IntakeOcrOperations_DocumentVersionId_SourceSha256",
            table: "IntakeOcrOperations",
            columns: new[] { "DocumentVersionId", "SourceSha256" });

        migrationBuilder.AddForeignKey(
            name: "FK_IntakeOcrOperations_DocumentVersions_DocumentVersionId",
            table: "IntakeOcrOperations",
            column: "DocumentVersionId",
            principalTable: "DocumentVersions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
