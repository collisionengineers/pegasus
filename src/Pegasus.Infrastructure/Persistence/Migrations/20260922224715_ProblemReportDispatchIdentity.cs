using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProblemReportDispatchIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProblemReports_Status",
                table: "ProblemReports");

            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "ProblemReports",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ProblemReports",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ProblemReports_OperationKey",
                table: "ProblemReports",
                column: "OperationKey",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProblemReports_Status",
                table: "ProblemReports",
                sql: "[Status] IN ('Sent', 'NotSent', 'Unknown')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProblemReports_OperationKey",
                table: "ProblemReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProblemReports_Status",
                table: "ProblemReports");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "ProblemReports");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ProblemReports");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProblemReports_Status",
                table: "ProblemReports",
                sql: "[Status] IN ('Sent', 'NotSent')");
        }
    }
}
