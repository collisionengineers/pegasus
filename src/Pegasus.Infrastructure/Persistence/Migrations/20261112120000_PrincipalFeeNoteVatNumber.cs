using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Principal may carry the VAT number its fee notes print (operator,
/// 12 November 2026; R1006): one nullable column, null meaning the standard number,
/// which is what every Principal starts with. Additive: no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261112120000_PrincipalFeeNoteVatNumber")]
public partial class PrincipalFeeNoteVatNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FeeNoteVatNumber",
            table: "Principals",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FeeNoteVatNumber", table: "Principals");
    }
}
