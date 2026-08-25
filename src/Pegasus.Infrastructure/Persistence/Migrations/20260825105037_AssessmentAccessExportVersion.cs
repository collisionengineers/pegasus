using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssessmentAccessExportVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LatestExportedWorkflowVersion",
                table: "EvaFirstHandoffProxies",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_EvaFirstHandoffProxies_ExportVersion",
                table: "EvaFirstHandoffProxies",
                sql: "[LatestExportedWorkflowVersion] IS NULL OR [LatestExportedWorkflowVersion] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EvaFirstHandoffProxies_ExportVersion",
                table: "EvaFirstHandoffProxies");

            migrationBuilder.DropColumn(
                name: "LatestExportedWorkflowVersion",
                table: "EvaFirstHandoffProxies");
        }
    }
}
