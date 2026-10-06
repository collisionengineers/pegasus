using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Glass's estimate a repair specification belongs to (operator,
/// 6 October 2026): the stock vehicle it stands on, the estimate id that
/// vehicle last answered, and the type number, placeholder flag, registration
/// and mileage the vehicle was proved against. All six are set together or
/// not at all; a specification made before this carries none and starts a new
/// estimate. Additive; the Web role's table-level grant on
/// <c>CaseRepairSpecifications</c> covers the columns.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261006150000_RepairSpecificationGlassEstimate")]
public partial class RepairSpecificationGlassEstimate : Migration
{
    private const string Table = "CaseRepairSpecifications";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "GlassVehicleId", table: Table, type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "GlassEstimateId", table: Table, type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "GlassNatCode", table: Table, type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<bool>(
            name: "GlassPlaceholder", table: Table, type: "bit", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "GlassRegistration", table: Table, type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<long>(
            name: "GlassMileageMiles", table: Table, type: "bigint", nullable: true);
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseRepairSpecifications_Glass",
            table: Table,
            sql: "([GlassVehicleId] IS NULL AND [GlassEstimateId] IS NULL AND [GlassNatCode] IS NULL "
                + "AND [GlassPlaceholder] IS NULL AND [GlassRegistration] IS NULL AND [GlassMileageMiles] IS NULL) OR "
                + "([GlassVehicleId] IS NOT NULL AND [GlassEstimateId] IS NOT NULL AND [GlassNatCode] IS NOT NULL "
                + "AND [GlassPlaceholder] IS NOT NULL AND [GlassRegistration] IS NOT NULL AND [GlassMileageMiles] IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_CaseRepairSpecifications_Glass", table: Table);
        migrationBuilder.DropColumn(name: "GlassMileageMiles", table: Table);
        migrationBuilder.DropColumn(name: "GlassRegistration", table: Table);
        migrationBuilder.DropColumn(name: "GlassPlaceholder", table: Table);
        migrationBuilder.DropColumn(name: "GlassNatCode", table: Table);
        migrationBuilder.DropColumn(name: "GlassEstimateId", table: Table);
        migrationBuilder.DropColumn(name: "GlassVehicleId", table: Table);
    }
}
