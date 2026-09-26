using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Add evidence on a Case page declares the upload's Case before the upload
/// (operator, 26 September 2026; FRD-18). The declaration travels with the
/// file: staged with it, then copied to its processed receipt, where the
/// Worker links the file to that Case and files it there. Both columns are
/// nullable; every other route leaves them empty. No foreign key: a Case is
/// never deleted, a declaration whose Case is gone reads as unavailable, and
/// the intake wipe keeps its existing order.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260926090000_DeclaredUploadDestination")]
public partial class DeclaredUploadDestination : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DeclaredCaseId",
            table: "IntakeStagedReceipts",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeclaredCaseId",
            table: "IntakeReceipts",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_IntakeStagedReceipts_DeclaredCaseId",
            table: "IntakeStagedReceipts",
            column: "DeclaredCaseId");

        migrationBuilder.CreateIndex(
            name: "IX_IntakeReceipts_DeclaredCaseId",
            table: "IntakeReceipts",
            column: "DeclaredCaseId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_IntakeReceipts_DeclaredCaseId",
            table: "IntakeReceipts");

        migrationBuilder.DropIndex(
            name: "IX_IntakeStagedReceipts_DeclaredCaseId",
            table: "IntakeStagedReceipts");

        migrationBuilder.DropColumn(
            name: "DeclaredCaseId",
            table: "IntakeReceipts");

        migrationBuilder.DropColumn(
            name: "DeclaredCaseId",
            table: "IntakeStagedReceipts");
    }
}
