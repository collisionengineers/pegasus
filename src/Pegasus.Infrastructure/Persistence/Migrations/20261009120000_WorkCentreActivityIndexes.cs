using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Work Centre's Activity figures read workflow events by type and time,
/// staff mail by purpose, state and sent time, and intake receipts by channel
/// and arrival time, on every load and refresh. Each read gets an index that
/// answers it without scanning the table. Additive: three indexes, no data
/// change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261009120000_WorkCentreActivityIndexes")]
public partial class WorkCentreActivityIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_EventType_OccurredAtUtc",
            table: "CaseWorkflowEvents",
            columns: new[] { "EventType", "OccurredAtUtc" })
            .Annotation("SqlServer:Include", new[] { "CaseId" });

        migrationBuilder.CreateIndex(
            name: "IX_StaffMailSendOperations_Purpose_State_ObservedSentAtUtc",
            table: "StaffMailSendOperations",
            columns: new[] { "Purpose", "State", "ObservedSentAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeReceipts_SourceChannel_ReceivedAtUtc",
            table: "IntakeReceipts",
            columns: new[] { "SourceChannel", "ReceivedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_IntakeReceipts_SourceChannel_ReceivedAtUtc",
            table: "IntakeReceipts");

        migrationBuilder.DropIndex(
            name: "IX_StaffMailSendOperations_Purpose_State_ObservedSentAtUtc",
            table: "StaffMailSendOperations");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_EventType_OccurredAtUtc",
            table: "CaseWorkflowEvents");
    }
}
