using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StandaloneAuditReportDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StandaloneAuditReportAssessment",
                table: "IntakeMailClassificationDecisions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StandaloneAuditReportAssetSourceLabel",
                table: "IntakeMailClassificationDecisions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StandaloneAuditReportAssessment",
                table: "IntakeMailClassificationDecisions");

            migrationBuilder.DropColumn(
                name: "StandaloneAuditReportAssetSourceLabel",
                table: "IntakeMailClassificationDecisions");
        }
    }
}
