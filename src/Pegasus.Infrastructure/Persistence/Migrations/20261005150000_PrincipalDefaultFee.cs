using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Principal's default fee (5 October 2026): the agreed fee a new Case of
/// the Principal starts with. Every existing Principal takes the column's
/// default of £180.00. Additive; the runtime roles' table-level grants on
/// <c>Principals</c> cover the column.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261005150000_PrincipalDefaultFee")]
public partial class PrincipalDefaultFee : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "DefaultFee",
            table: "Principals",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 180m);
        migrationBuilder.AddCheckConstraint(
            name: "CK_Principals_DefaultFee",
            table: "Principals",
            sql: "[DefaultFee] > 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_Principals_DefaultFee", table: "Principals");
        migrationBuilder.DropColumn(name: "DefaultFee", table: "Principals");
    }
}
