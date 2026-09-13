using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// "Notes on every Case" (Contacts planning, 13 September): a Principal record
/// and a Claim source record each carry one text an Administrator writes once,
/// and every Case of that Principal or from that Claim source shows it read-only
/// on its Overview, read live from the record and never copied. One column on
/// the organisation serves both doors — the Principal settings dialog and the
/// Contact dialog edit the same record — and other contact types keep it empty.
/// The Case's own notes beside them are ordinary case-data rows and need no
/// column.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913090111_OrganizationNotesOnEveryCase")]
public partial class OrganizationNotesOnEveryCase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NotesOnEveryCase",
            table: "Organizations",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "NotesOnEveryCase",
            table: "Organizations");
    }
}
