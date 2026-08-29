using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// The scaffolder also proposed adding CaseWorkflows.EditLeaseHolderKind,
    /// which 20260828110108_CaseEditLeaseHolderKind already creates. That
    /// migration reached this branch by merge and carries an *earlier*
    /// timestamp than this branch's own migrations, so the last Designer
    /// snapshot here (GrantProviderSubmissions) predates it and the diff saw
    /// the column as missing. The duplicate AddColumn is removed by hand;
    /// PegasusDbContextModelSnapshot is correct and carries the column.
    /// </remarks>
    public partial class ProviderDeclaredInstruction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields");

            migrationBuilder.AddColumn<string>(
                name: "DeclaredInstructionJson",
                table: "ProviderSubmissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "StagedReceiptId",
                table: "ProviderSubmissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimantAddress",
                table: "InstructionDrafts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimantContactNumber",
                table: "InstructionDrafts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHandlerEmailAddress",
                table: "InstructionDrafts",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHandlerName",
                table: "InstructionDrafts",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileHandlerPhoneNumber",
                table: "InstructionDrafts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "InstructionDrafts",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VatStatus",
                table: "InstructionDrafts",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleMileageUnit",
                table: "InstructionDrafts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields",
                sql: "[FieldName] IN ('work_provider_code', 'claimant_name', 'claimant_contact_number', 'claimant_address', 'claim_number', 'vehicle_registration', 'vehicle_make', 'vehicle_model', 'vehicle_mileage', 'vehicle_mileage_unit', 'accident_circumstances', 'incident_date', 'contact_name', 'contact_email_address', 'contact_phone_number', 'instruction_date', 'vat_status', 'inspection_date', 'inspection_deadline', 'inspection_address', 'inspection_mode')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields",
                sql: "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'provider_setting', 'provider_api')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields");

            migrationBuilder.DropColumn(
                name: "DeclaredInstructionJson",
                table: "ProviderSubmissions");

            migrationBuilder.DropColumn(
                name: "StagedReceiptId",
                table: "ProviderSubmissions");

            migrationBuilder.DropColumn(
                name: "ClaimantAddress",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "ClaimantContactNumber",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "FileHandlerEmailAddress",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "FileHandlerName",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "FileHandlerPhoneNumber",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "VatStatus",
                table: "InstructionDrafts");

            migrationBuilder.DropColumn(
                name: "VehicleMileageUnit",
                table: "InstructionDrafts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_FieldName",
                table: "CaseDataFields",
                sql: "[FieldName] IN ('work_provider_code', 'claimant_name', 'claim_number', 'vehicle_registration', 'vehicle_make', 'vehicle_model', 'vehicle_mileage', 'vehicle_mileage_unit', 'accident_circumstances', 'incident_date', 'contact_name', 'contact_email_address', 'contact_phone_number', 'instruction_date', 'vat_status', 'inspection_date', 'inspection_deadline', 'inspection_address', 'inspection_mode')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields",
                sql: "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'provider_setting')");
        }
    }
}
