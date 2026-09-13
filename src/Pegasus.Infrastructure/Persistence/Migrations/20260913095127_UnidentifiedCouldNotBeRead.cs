using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Unidentified widens (Received file D5, Q3): a file processing could not read
/// becomes an Unidentified item with the reason Could not be read and the kind
/// of file it was, so it lands in the work list and ages like any other; and
/// every item names where its material sits — the retained e-mail (Open
/// message) and the original file (Open file) — because the received-file page
/// that used to show them is gone. Existing rows keep their older reason codes;
/// those are still names in the same vocabulary. Both runtime roles already
/// insert and update this table, so no grant changes.
///
/// Close with reason is a resolution with the new Closed target kind and needs
/// no column: the item keeps its U-reference and sits under the Closed filter.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913095127_UnidentifiedCouldNotBeRead")]
public partial class UnidentifiedCouldNotBeRead : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "FileKind",
            table: "UnidentifiedItems",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceAssetId",
            table: "UnidentifiedItems",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceMessageId",
            table: "UnidentifiedItems",
            type: "uniqueidentifier",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FileKind", table: "UnidentifiedItems");
        migrationBuilder.DropColumn(name: "SourceAssetId", table: "UnidentifiedItems");
        migrationBuilder.DropColumn(name: "SourceMessageId", table: "UnidentifiedItems");
    }
}
