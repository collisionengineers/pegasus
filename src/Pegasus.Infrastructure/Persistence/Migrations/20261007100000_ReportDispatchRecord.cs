using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// What report dispatch keeps (operator, 6 October 2026). A report send records
/// staff's answers, the holds they ticked, a Stop they overrode and the
/// after-send list on its own operation row
/// (<c>StaffMailSendOperations.ReportDispatchJson</c>), since a report is sent in
/// one step and that row is the record of what was sent. A recognised filed
/// estimate keeps the format it was read in
/// (<c>DocumentVersions.RecognisedEstimateProvider</c>), so the delivery can tell
/// an Audatex estimate from any other. A Case task's description loses its
/// 500-character limit. Additive: two nullable columns and one column widened.
/// The runtime roles' table-level grants cover all three.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007100000_ReportDispatchRecord")]
public partial class ReportDispatchRecord : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ReportDispatchJson", table: "StaffMailSendOperations",
            type: "nvarchar(max)", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "RecognisedEstimateProvider", table: "DocumentVersions",
            type: "nvarchar(100)", maxLength: 100, nullable: true);
        // The not-empty check reads the column, so it steps aside while the
        // column is widened.
        migrationBuilder.DropCheckConstraint(name: "CK_CaseTasks_Description", table: "CaseTasks");
        migrationBuilder.AlterColumn<string>(
            name: "Description", table: "CaseTasks",
            type: "nvarchar(max)", nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(500)", oldMaxLength: 500);
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseTasks_Description", table: "CaseTasks", sql: "[Description] <> ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: "CK_CaseTasks_Description", table: "CaseTasks");
        migrationBuilder.AlterColumn<string>(
            name: "Description", table: "CaseTasks",
            type: "nvarchar(500)", maxLength: 500, nullable: false,
            oldClrType: typeof(string), oldType: "nvarchar(max)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseTasks_Description", table: "CaseTasks", sql: "[Description] <> ''");
        migrationBuilder.DropColumn(name: "RecognisedEstimateProvider", table: "DocumentVersions");
        migrationBuilder.DropColumn(name: "ReportDispatchJson", table: "StaffMailSendOperations");
    }
}
