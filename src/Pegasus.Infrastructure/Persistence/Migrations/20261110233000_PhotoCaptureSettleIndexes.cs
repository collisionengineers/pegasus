using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The photo capture settle's two reads that run every minute (FRD-28; R838).
/// Its listing of captures due to settle ORs a send, an expiry and a
/// cancellation, which no index on those columns can seek, and orders by
/// <c>(CreatedAtUtc, Id)</c> from its cursor, which no index held, so SQL
/// scanned and sorted every capture; the new index is that order with the
/// tested columns included. Its reach reads the grouped batches not yet
/// reached; the new filtered index holds only those, beside the unique group
/// index the record's merge reads. Additive: two indexes are created, and no
/// row or existing index changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261110233000_PhotoCaptureSettleIndexes")]
public partial class PhotoCaptureSettleIndexes : Migration
{
    private const string Captures = "PhotoCaptures";
    private const string CapturesIndex = "IX_PhotoCaptures_CreatedAtUtc_Id";
    private const string Batches = "PhotoCaptureBatches";
    private const string BatchesIndex = "IX_PhotoCaptureBatches_SubmissionGroupId_Unreached";
    private static readonly string[] CapturesColumns = ["CreatedAtUtc", "Id"];
    private static readonly string[] CapturesIncluded = ["SettledBatches", "SentAtUtc", "SettledAtUtc", "ExpiresAtUtc", "RevokedAtUtc"];
    private static readonly string[] BatchesIncluded = ["ClosedAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
                name: CapturesIndex,
                table: Captures,
                columns: CapturesColumns)
            .Annotation("SqlServer:Include", CapturesIncluded);

        migrationBuilder.CreateIndex(
                name: BatchesIndex,
                table: Batches,
                column: "SubmissionGroupId",
                filter: "[SubmissionGroupId] IS NOT NULL AND [ReachedAtUtc] IS NULL")
            .Annotation("SqlServer:Include", BatchesIncluded);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: BatchesIndex, table: Batches);
        migrationBuilder.DropIndex(name: CapturesIndex, table: Captures);
    }
}
