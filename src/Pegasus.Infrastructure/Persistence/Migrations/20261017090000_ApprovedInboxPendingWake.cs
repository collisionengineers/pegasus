using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// When a mailbox wake last found the mailbox leased or not yet due, so the
/// recovery timer polls it on its next minute instead of dropping the wake
/// (operator ruling of 15 October 2026). Additive. The runtime roles'
/// table-level grants on <c>ApprovedInboxPollStates</c> cover the column, so no
/// grant changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261017090000_ApprovedInboxPendingWake")]
public partial class ApprovedInboxPendingWake : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PendingWakeAtUtc", table: "ApprovedInboxPollStates",
            type: "datetimeoffset", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "PendingWakeAtUtc", table: "ApprovedInboxPollStates");
    }
}
