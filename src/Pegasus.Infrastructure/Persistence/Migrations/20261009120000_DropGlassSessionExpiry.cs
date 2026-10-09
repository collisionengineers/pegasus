using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Glass's session has no lifetime (operator, 9 October 2026; issue 1067):
/// a saved return lands and a press continues the staff member's own session
/// however long ago it started, so nothing reads the expiry a launch recorded
/// and its column goes. Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261009120000_DropGlassSessionExpiry")]
public partial class DropGlassSessionExpiry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropColumn(
            name: "ExpiresAtUtc",
            table: "GlassRepairEstimateSessions");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "DropGlassSessionExpiry is forward-only: it drops GlassRepairEstimateSessions.ExpiresAtUtc. Restore from an approved backup instead.");
}
