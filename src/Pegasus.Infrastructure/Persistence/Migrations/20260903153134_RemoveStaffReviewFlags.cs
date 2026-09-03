using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStaffReviewFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireStaffImageReviewBeforeEngineerAssignment",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "RequireStaffInstructionReviewBeforeEngineerAssignment",
                table: "WorkflowConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireStaffImageReviewBeforeEngineerAssignment",
                table: "WorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireStaffInstructionReviewBeforeEngineerAssignment",
                table: "WorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.UpdateData(
                table: "WorkflowConfigurations",
                keyColumn: "Id",
                keyValue: "case-workflow",
                columns: new[] { "RequireStaffImageReviewBeforeEngineerAssignment", "RequireStaffInstructionReviewBeforeEngineerAssignment" },
                values: new object[] { true, true });
        }
    }
}
