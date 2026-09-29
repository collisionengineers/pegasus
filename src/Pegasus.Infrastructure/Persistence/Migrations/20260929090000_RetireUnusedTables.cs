using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Retires three stores nothing reads or writes. <c>VehicleConfirmations</c>:
/// the last reader went with the write-only confirmation history and no code
/// writes it (#842). <c>AiWorkRequests</c>: the push hand-off that tracked it
/// has no caller, so the AI Job List is the only Send to AI route (#843).
/// The connector settings that only that hand-off read (address, timeout,
/// protected token and its rotation time) leave the <c>SendToAiControl</c>
/// row; the Send to AI switch stays. SQL Server drops a table's permission
/// rows with the table, so the retired tables need no REVOKE.
/// Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260929090000_RetireUnusedTables")]
public partial class RetireUnusedTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropTable(
            name: "VehicleConfirmations");

        migrationBuilder.DropTable(
            name: "AiWorkRequests");

        migrationBuilder.DropColumn(
            name: "ChannelBaseUrl",
            table: "SendToAiControl");

        migrationBuilder.DropColumn(
            name: "ChannelTokenProtected",
            table: "SendToAiControl");

        migrationBuilder.DropColumn(
            name: "TimeoutSeconds",
            table: "SendToAiControl");

        migrationBuilder.DropColumn(
            name: "TokenRotatedAtUtc",
            table: "SendToAiControl");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RetireUnusedTables is forward-only: it drops VehicleConfirmations and AiWorkRequests and the SendToAiControl connector columns. Restore from an approved backup instead.");
}
