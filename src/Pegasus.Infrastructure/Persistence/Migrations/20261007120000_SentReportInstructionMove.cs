using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Whether the instruction e-mail a confirmed report send answered has been moved
/// to Deleted Items by the Worker (ADR-0063, 5 October 2026): a state that stays
/// NULL while the tidy is due, an attempt count, when it moved and the last failure
/// code. Additive. The runtime roles' table-level grants on
/// <c>StaffMailSendOperations</c> cover the columns, so no grant changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007120000_SentReportInstructionMove")]
public partial class SentReportInstructionMove : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InstructionMoveState", table: "StaffMailSendOperations",
            type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<int>(
            name: "InstructionMoveAttempts", table: "StaffMailSendOperations",
            type: "int", nullable: false, defaultValue: 0);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "InstructionMovedAtUtc", table: "StaffMailSendOperations",
            type: "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "InstructionMoveFailureCode", table: "StaffMailSendOperations",
            type: "nvarchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_StaffMailSendOperations_Purpose_State",
            table: "StaffMailSendOperations",
            columns: new[] { "Purpose", "State" },
            filter: "[InstructionMoveState] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StaffMailSendOperations_Purpose_State",
            table: "StaffMailSendOperations");
        migrationBuilder.DropColumn(name: "InstructionMoveFailureCode", table: "StaffMailSendOperations");
        migrationBuilder.DropColumn(name: "InstructionMovedAtUtc", table: "StaffMailSendOperations");
        migrationBuilder.DropColumn(name: "InstructionMoveAttempts", table: "StaffMailSendOperations");
        migrationBuilder.DropColumn(name: "InstructionMoveState", table: "StaffMailSendOperations");
    }
}
