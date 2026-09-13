using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Personal notifications (Work Centre D10): the bell becomes a per-person list
/// with three defined causes — an AI draft ready on a Case, a Case assigned to
/// an engineer, and a Case the engineer holds changing hands (edited by someone
/// else, an e-mail, a query). One row per person per event, carrying the Case,
/// the cause and the relative route the shell opens; opened rows are marked
/// read, nothing is deleted by a person, and rows older than 30 days drop off.
///
/// Both runtime roles write: Web raises assignment, edit, staff link and AI
/// draft rows and marks them read; the Worker raises the rows for e-mail it
/// links to a Case and purges the ones past retention, so it alone deletes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913092038_StaffNotifications")]
public partial class StaffNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "StaffNotifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Registration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                Cause = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Route = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                RaisedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StaffNotifications", x => x.Id);
                table.CheckConstraint("CK_StaffNotifications_Cause", "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived')");
                table.ForeignKey(
                    name: "FK_StaffNotifications_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StaffNotifications_CaseId",
            table: "StaffNotifications",
            column: "CaseId");

        migrationBuilder.CreateIndex(
            name: "IX_StaffNotifications_RaisedAtUtc",
            table: "StaffNotifications",
            column: "RaisedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_StaffNotifications_StaffId_RaisedAtUtc",
            table: "StaffNotifications",
            columns: new[] { "StaffId", "RaisedAtUtc" },
            descending: new[] { false, true });

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[StaffNotifications] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[StaffNotifications] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[StaffNotifications] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StaffNotifications");
    }
}
