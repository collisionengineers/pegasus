using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909144000_PrincipalReportGenerationPolicies")]
public partial class PrincipalReportGenerationPolicies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ReportGenerationPolicy", table: "Principals", type: "nvarchar(40)", nullable: false, defaultValue: "Pegasus");
        migrationBuilder.AddColumn<bool>(name: "IncludeOriginalInstructionSender", table: "Principals", type: "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "ReportRecipientAddressesJson", table: "Principals", type: "nvarchar(max)", nullable: false, defaultValue: "[]");
        migrationBuilder.DropColumn(name: "EvaManualSubmission", table: "Principals");
        migrationBuilder.AddCheckConstraint(name: "CK_Principals_ReportGenerationPolicy", table: "Principals", sql: "[ReportGenerationPolicy] IN ('Pegasus', 'EvaZip', 'EvaManualApi', 'EvaAutomaticApiOnReview')");
        migrationBuilder.CreateTable(
            name: "AutomaticEvaReviewSubmissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkflowVersion = table.Column<long>(type: "bigint", nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                State = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LeaseToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AutomaticEvaReviewSubmissions", x => x.Id);
                table.CheckConstraint("CK_AutomaticEvaReviewSubmissions_State", "[State] IN ('Pending', 'Dispatching', 'Completed', 'ReconciliationRequired')");
            });
        migrationBuilder.CreateIndex(name: "IX_AutomaticEvaReviewSubmissions_CaseId", table: "AutomaticEvaReviewSubmissions", column: "CaseId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_AutomaticEvaReviewSubmissions_State_DueAtUtc", table: "AutomaticEvaReviewSubmissions", columns: new[] { "State", "DueAtUtc" });
        migrationBuilder.Sql("""
            IF DATABASE_PRINCIPAL_ID('pegasus_worker_runtime_role') IS NOT NULL
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[AutomaticEvaReviewSubmissions] TO [pegasus_worker_runtime_role];
            IF DATABASE_PRINCIPAL_ID('pegasus_web_runtime_role') IS NOT NULL
                GRANT SELECT, INSERT ON OBJECT::[dbo].[AutomaticEvaReviewSubmissions] TO [pegasus_web_runtime_role];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("This pre-release report policy migration is forward-only.");
}
