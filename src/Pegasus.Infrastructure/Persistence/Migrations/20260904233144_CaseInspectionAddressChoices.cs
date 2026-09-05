using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseInspectionAddressChoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields",
                sql: "[FieldName] IN ('work_provider_code', 'claimant_name', 'claimant_contact_number', 'claimant_address', 'claim_number', 'vehicle_registration', 'vehicle_make', 'vehicle_model', 'vehicle_mileage', 'vehicle_mileage_unit', 'accident_circumstances', 'incident_date', 'contact_name', 'contact_email_address', 'contact_phone_number', 'instruction_date', 'vat_status', 'inspection_date', 'inspection_deadline', 'inspection_address', 'inspection_mode', 'storage_location')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields",
                sql: "[FieldName] IN ('work_provider_code', 'claimant_name', 'claimant_contact_number', 'claimant_address', 'claim_number', 'vehicle_registration', 'vehicle_make', 'vehicle_model', 'vehicle_mileage', 'vehicle_mileage_unit', 'accident_circumstances', 'incident_date', 'contact_name', 'contact_email_address', 'contact_phone_number', 'instruction_date', 'vat_status', 'inspection_date', 'inspection_deadline', 'inspection_address', 'inspection_mode')");
        }
    }
}
