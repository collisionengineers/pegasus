using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Retains the raw DVLA classification signals used to derive the Case's
/// vehicle type. Existing observations remain valid with no inferred values.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260916090000_VehicleLookupTypeSignals")]
public partial class VehicleLookupTypeSignals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "RevenueWeightKg",
            table: "VehicleLookupObservations",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TypeApproval",
            table: "VehicleLookupObservations",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Wheelplan",
            table: "VehicleLookupObservations",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RevenueWeightKg",
            table: "VehicleLookupObservations");

        migrationBuilder.DropColumn(
            name: "TypeApproval",
            table: "VehicleLookupObservations");

        migrationBuilder.DropColumn(
            name: "Wheelplan",
            table: "VehicleLookupObservations");
    }
}
