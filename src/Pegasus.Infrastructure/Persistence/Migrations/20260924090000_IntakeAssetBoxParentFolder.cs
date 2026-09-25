using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// An intake asset records the Box folder that holds its confirmed copy, so a
/// reader expects exactly that parent: the holding folder for intake with no
/// settled destination, or the Case or Vehicle images folder that filed it
/// (operator, 23 September 2026: intake filed to a Case is not held).
/// Additive: one nullable column. Existing rows are pre-release test data and
/// are not backfilled.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260924090000_IntakeAssetBoxParentFolder")]
public partial class IntakeAssetBoxParentFolder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BoxParentFolderId",
            table: "IntakeAssets",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BoxParentFolderId",
            table: "IntakeAssets");
    }
}
