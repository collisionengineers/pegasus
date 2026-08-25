using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkflowCompletenessWaivers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireCompleteImagesBeforeEngineerAssignment",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "RequireCompleteInstructionsBeforeEngineerAssignment",
                table: "WorkflowConfigurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireCompleteImagesBeforeEngineerAssignment",
                table: "WorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireCompleteInstructionsBeforeEngineerAssignment",
                table: "WorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "WorkflowConfigurations",
                keyColumn: "Id",
                keyValue: "case-workflow",
                columns: new[] { "RequireCompleteImagesBeforeEngineerAssignment", "RequireCompleteInstructionsBeforeEngineerAssignment" },
                values: new object[] { true, true });
        }
    }
}
