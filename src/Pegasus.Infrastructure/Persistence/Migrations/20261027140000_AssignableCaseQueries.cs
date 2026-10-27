using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Case query is assignable (ADR-0085, operator 27 October 2026). Each row
/// gains who holds it and when, and open rows are indexed for the Work Centre.
/// The bell gains <c>QueryAssigned</c>, so the cause check is restated.
/// Additive: existing rows stay Unassigned.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261027140000_AssignableCaseQueries")]
public partial class AssignableCaseQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "AssigneeStaffId",
            table: "CaseQueries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "AssignedAtUtc",
            table: "CaseQueries",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CaseQueries_AssigneeStaffId_RaisedAtUtc",
            table: "CaseQueries",
            columns: new[] { "AssigneeStaffId", "RaisedAtUtc" },
            filter: "[ClosedAtUtc] IS NULL");

        migrationBuilder.DropCheckConstraint(name: "CK_StaffNotifications_Cause", table: "StaffNotifications");
        migrationBuilder.AddCheckConstraint(
            name: "CK_StaffNotifications_Cause",
            table: "StaffNotifications",
            sql: "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived', 'CancellationReceived', 'QueryAssigned')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM [dbo].[StaffNotifications] WHERE [Cause] = N'QueryAssigned';");
        migrationBuilder.DropCheckConstraint(name: "CK_StaffNotifications_Cause", table: "StaffNotifications");
        migrationBuilder.AddCheckConstraint(
            name: "CK_StaffNotifications_Cause",
            table: "StaffNotifications",
            sql: "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived', 'CancellationReceived')");

        migrationBuilder.DropIndex(name: "IX_CaseQueries_AssigneeStaffId_RaisedAtUtc", table: "CaseQueries");
        migrationBuilder.DropColumn(name: "AssignedAtUtc", table: "CaseQueries");
        migrationBuilder.DropColumn(name: "AssigneeStaffId", table: "CaseQueries");
    }
}
