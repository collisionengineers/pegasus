using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the per-principal inspection-mode setting (ADR-0018, 2026-08-03
    /// product-owner decision) and widens the case-data source kinds with
    /// 'provider_setting'. QDOS is seeded 'image_based_assessment' from the
    /// evidence workbook docs/reference/workproviders-and-repairers/
    /// providers-worked-on.xlsx, sheet "Final" ("Principal Inspection Address
    /// Frequency"): 7,408 of 7,415 QDOS cases were Image Based Assessment.
    /// The seed targets one row by its unique code and does not bump the
    /// principal's optimistic Version.
    /// </summary>
    public partial class ProviderInspectionModeSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields");

            migrationBuilder.AddColumn<string>(
                name: "InspectionMode",
                table: "Principals",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "physical_address");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Principals_InspectionMode",
                table: "Principals",
                sql: "[InspectionMode] IN ('physical_address', 'image_based_assessment')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields",
                sql: "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'provider_setting')");

            migrationBuilder.Sql(
                "UPDATE [Principals] SET [InspectionMode] = 'image_based_assessment' WHERE [Code] = 'QDOS';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Principals_InspectionMode",
                table: "Principals");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields");

            migrationBuilder.DropColumn(
                name: "InspectionMode",
                table: "Principals");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields",
                sql: "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup')");
        }
    }
}
