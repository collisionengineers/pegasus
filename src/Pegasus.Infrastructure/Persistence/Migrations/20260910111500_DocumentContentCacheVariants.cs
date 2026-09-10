using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// DOCS-015: the document content cache held one entry per source identity, so
/// a derived gallery thumbnail had nowhere to live — its row was refused by the
/// same unique index the content entry occupies, and would have been read back
/// as the content itself. <c>Variant</c> names the kind of object a row
/// records: the empty string for the durable content, and a rendering's key for
/// each derived object. It joins both unique indexes, so every kind keeps its
/// own single entry per source.
///
/// Existing rows are content entries and take the empty default; the cache is
/// derived data behind a durable Box custody record either way, so nothing is
/// backfilled and nothing is lost if a row is dropped.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260910111500_DocumentContentCacheVariants")]
public partial class DocumentContentCacheVariants : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_DocumentContentCacheEntries_DocumentVersionId",
            table: "DocumentContentCacheEntries");

        migrationBuilder.DropIndex(
            name: "IX_DocumentContentCacheEntries_IntakeAssetId",
            table: "DocumentContentCacheEntries");

        migrationBuilder.AddColumn<string>(
            name: "Variant",
            table: "DocumentContentCacheEntries",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentContentCacheEntries_DocumentVersionId_Variant",
            table: "DocumentContentCacheEntries",
            columns: new[] { "DocumentVersionId", "Variant" },
            unique: true,
            filter: "[DocumentVersionId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentContentCacheEntries_IntakeAssetId_Variant",
            table: "DocumentContentCacheEntries",
            columns: new[] { "IntakeAssetId", "Variant" },
            unique: true,
            filter: "[IntakeAssetId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_DocumentContentCacheEntries_DocumentVersionId_Variant",
            table: "DocumentContentCacheEntries");

        migrationBuilder.DropIndex(
            name: "IX_DocumentContentCacheEntries_IntakeAssetId_Variant",
            table: "DocumentContentCacheEntries");

        // Without the variant there is one entry per source again, so the
        // derived rows have to go before the index that says so is restored.
        // Their objects age out with the container's own cache lifecycle.
        migrationBuilder.Sql(
            "DELETE FROM [dbo].[DocumentContentCacheEntries] WHERE [Variant] <> ''");

        migrationBuilder.DropColumn(
            name: "Variant",
            table: "DocumentContentCacheEntries");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentContentCacheEntries_DocumentVersionId",
            table: "DocumentContentCacheEntries",
            column: "DocumentVersionId",
            unique: true,
            filter: "[DocumentVersionId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentContentCacheEntries_IntakeAssetId",
            table: "DocumentContentCacheEntries",
            column: "IntakeAssetId",
            unique: true,
            filter: "[IntakeAssetId] IS NOT NULL");
    }
}
