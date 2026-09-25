using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The DVLA/DVSA lookup keeps the vehicle's colour and tax due date beside the
/// engine and fuel it already kept, and the Worker's lookup fill becomes the
/// only writer of the Case's engine, fuel, colour, tax expiry and MOT expiry
/// (operator, 24 September 2026). A complete answer that no longer carries one
/// of those facts clears it, so the Worker role may delete CaseAssessmentFields
/// rows. Additive: two nullable columns and one grant; existing observations
/// stay valid with no inferred values.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260925090000_VehicleLookupDerivedFacts")]
public partial class VehicleLookupDerivedFacts : Migration
{
    private const string WorkerRole = "pegasus_worker_runtime_role";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Colour", table: "VehicleLookupObservations", type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "TaxDueDate", table: "VehicleLookupObservations", type: "date", nullable: true);
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql($"GRANT DELETE ON OBJECT::[dbo].[CaseAssessmentFields] TO [{WorkerRole}];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql($"REVOKE DELETE ON OBJECT::[dbo].[CaseAssessmentFields] FROM [{WorkerRole}];");
        }
        migrationBuilder.DropColumn(name: "TaxDueDate", table: "VehicleLookupObservations");
        migrationBuilder.DropColumn(name: "Colour", table: "VehicleLookupObservations");
    }
}
