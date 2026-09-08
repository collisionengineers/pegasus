using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

public partial class RemoveCaseStaffConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InstructionConfirmedByStaff",
            table: "Cases");
        migrationBuilder.DropColumn(
            name: "ImagesConfirmedByStaff",
            table: "Cases");
        migrationBuilder.DropColumn(
            name: "InstructionConfirmedByStaff",
            table: "IntakeAllocationAttempts");
        migrationBuilder.DropColumn(
            name: "ImagesConfirmedByStaff",
            table: "IntakeAllocationAttempts");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "InstructionConfirmedByStaff",
            table: "Cases",
            type: "bit",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "ImagesConfirmedByStaff",
            table: "Cases",
            type: "bit",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "InstructionConfirmedByStaff",
            table: "IntakeAllocationAttempts",
            type: "bit",
            nullable: false,
            defaultValue: false);
        migrationBuilder.AddColumn<bool>(
            name: "ImagesConfirmedByStaff",
            table: "IntakeAllocationAttempts",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }
}
