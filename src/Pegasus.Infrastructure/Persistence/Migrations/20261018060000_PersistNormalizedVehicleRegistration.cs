using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The automatic vehicle lookup sweep runs every minute and compared a coarse
/// form of every Case's registration (no spaces or hyphens), worked out in the
/// query, with the lookup requests. The coarse form is now a stored column that
/// the (FieldName, ValueKind) index carries, and the requests gain an index on
/// (CaseId, Registration). Additive: one computed column and two indexes, no
/// data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261018060000_PersistNormalizedVehicleRegistration")]
public partial class PersistNormalizedVehicleRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NormalizedRegistration",
            table: "CaseDataFields",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true,
            computedColumnSql: "CONVERT(nvarchar(20), CASE WHEN [FieldName] = N'vehicle_registration' AND LEN(REPLACE(REPLACE([Value], N' ', N''), N'-', N'')) <= 20 THEN REPLACE(REPLACE([Value], N' ', N''), N'-', N'') END)",
            stored: true);

        migrationBuilder.DropIndex(
            name: "IX_CaseDataFields_FieldName_ValueKind",
            table: "CaseDataFields");

        migrationBuilder.CreateIndex(
            name: "IX_CaseDataFields_FieldName_ValueKind",
            table: "CaseDataFields",
            columns: new[] { "FieldName", "ValueKind" })
            .Annotation("SqlServer:Include", new[] { "NormalizedRegistration" });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleLookupRequests_CaseId_Registration",
            table: "VehicleLookupRequests",
            columns: new[] { "CaseId", "Registration" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_VehicleLookupRequests_CaseId_Registration",
            table: "VehicleLookupRequests");

        migrationBuilder.DropIndex(
            name: "IX_CaseDataFields_FieldName_ValueKind",
            table: "CaseDataFields");

        migrationBuilder.DropColumn(
            name: "NormalizedRegistration",
            table: "CaseDataFields");

        migrationBuilder.CreateIndex(
            name: "IX_CaseDataFields_FieldName_ValueKind",
            table: "CaseDataFields",
            columns: new[] { "FieldName", "ValueKind" });
    }
}
