using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Principal's salvage matrix (29 September 2026): the bands stored as
/// JSON on the Principal row, NULL for a Principal without one. Additive; the
/// runtime roles' table-level grants on <c>Principals</c> cover the column.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260930090000_PrincipalSalvageMatrix")]
public partial class PrincipalSalvageMatrix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "SalvageMatrixJson", table: "Principals", type: "nvarchar(max)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "SalvageMatrixJson", table: "Principals");
    }
}
