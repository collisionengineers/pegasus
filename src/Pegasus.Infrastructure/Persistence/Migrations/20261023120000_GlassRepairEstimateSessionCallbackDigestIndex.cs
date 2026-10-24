using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Glass's callback finds its session by the fingerprint of its one-use
/// correlation (<c>CallbackDigest</c>), a column no index covered, so each
/// callback scanned the session table. The index answers that lookup with a
/// seek. Additive: one non-unique index, no data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261023120000_GlassRepairEstimateSessionCallbackDigestIndex")]
public partial class GlassRepairEstimateSessionCallbackDigestIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_GlassRepairEstimateSessions_CallbackDigest",
            table: "GlassRepairEstimateSessions",
            column: "CallbackDigest");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_GlassRepairEstimateSessions_CallbackDigest",
            table: "GlassRepairEstimateSessions");
    }
}
