using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A file uploaded to a declared Case carries the type the member of staff chose for it, from the
/// staged receipt to the processed one, so the Worker files it as that type (FRD-18; R559, R560,
/// 30 October 2026). Additive: one nullable name column on each receipt table, no row changes; a
/// receipt staged before it has no choice and files as Other.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261030120000_DeclaredUploadDocumentType")]
public partial class DeclaredUploadDocumentType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DeclaredDocumentType",
            table: "IntakeStagedReceipts",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeclaredDocumentType",
            table: "IntakeReceipts",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DeclaredDocumentType",
            table: "IntakeStagedReceipts");

        migrationBuilder.DropColumn(
            name: "DeclaredDocumentType",
            table: "IntakeReceipts");
    }
}
