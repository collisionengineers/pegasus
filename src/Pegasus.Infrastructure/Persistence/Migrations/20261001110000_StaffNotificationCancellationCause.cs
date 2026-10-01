using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The bell gains one cause, <c>CancellationReceived</c>: a cancellation linked
/// to, or corrected onto, an open Case the engineer is assigned to (FRD-12,
/// 1 October 2026). The cause column's check constraint lists the causes, so
/// it is restated with the new one.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261001110000_StaffNotificationCancellationCause")]
public partial class StaffNotificationCancellationCause : Migration
{
    private const string Constraint = "CK_StaffNotifications_Cause";
    private const string Table = "StaffNotifications";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: Constraint, table: Table);
        migrationBuilder.AddCheckConstraint(
            name: Constraint,
            table: Table,
            sql: "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived', 'CancellationReceived')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM [dbo].[StaffNotifications] WHERE [Cause] = N'CancellationReceived';");
        migrationBuilder.DropCheckConstraint(name: Constraint, table: Table);
        migrationBuilder.AddCheckConstraint(
            name: Constraint,
            table: Table,
            sql: "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived')");
    }
}
