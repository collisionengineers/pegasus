using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Problem reports (ADR-0055): a staff member's words and the state captured
/// with them, kept before the report is raised as a repository issue, with
/// where it went or why it could not be sent. Web alone writes the table and
/// is denied DELETE; the Worker has no part in it and is denied DELETE too.
/// </summary>
public partial class ProblemReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "ProblemReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Route = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                IssueNumber = table.Column<int>(type: "int", nullable: true),
                IssueUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                Failure = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProblemReports", x => x.Id);
                table.CheckConstraint("CK_ProblemReports_Status", "[Status] IN ('Sent', 'NotSent')");
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProblemReports_CreatedAtUtc",
            table: "ProblemReports",
            column: "CreatedAtUtc",
            descending: []);

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ProblemReports] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[ProblemReports] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                DENY DELETE ON OBJECT::[dbo].[ProblemReports] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ProblemReports");
    }
}
