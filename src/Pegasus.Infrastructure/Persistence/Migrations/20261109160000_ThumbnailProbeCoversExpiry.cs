using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Worker's thumbnail candidate read asks, for every image version filed in
/// the last 30 days, whether it has a live cache entry of a variant. The
/// unique index on <c>(DocumentVersionId, Variant)</c> did not carry
/// <c>ExpiresAtUtc</c>, so SQL scanned the whole table once per version
/// (#2003; operator, 9 November 2026, R786). The index is rebuilt with the
/// expiry included. Additive: the same keys, filter and uniqueness, and no
/// row changes (the DropIndex and CreateIndex pair, as in R432).
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261109160000_ThumbnailProbeCoversExpiry")]
public partial class ThumbnailProbeCoversExpiry : Migration
{
    private const string Table = "DocumentContentCacheEntries";
    private const string Index = "IX_DocumentContentCacheEntries_DocumentVersionId_Variant";
    private const string Filter = "[DocumentVersionId] IS NOT NULL";
    private static readonly string[] Columns = ["DocumentVersionId", "Variant"];
    private static readonly string[] Included = ["ExpiresAtUtc"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: Index, table: Table);

        migrationBuilder.CreateIndex(
                name: Index,
                table: Table,
                columns: Columns,
                unique: true,
                filter: Filter)
            .Annotation("SqlServer:Include", Included);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: Index, table: Table);

        migrationBuilder.CreateIndex(
            name: Index,
            table: Table,
            columns: Columns,
            unique: true,
            filter: Filter);
    }
}
