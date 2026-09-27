using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The image tag decides how an image in the report prints, and whether the
/// report uses an image is a plain on/off (operator, 26 September 2026;
/// FRD-06). Each occurrence gains the InReport flag, on for a new image, and
/// the stored report role goes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260927004150_ImageInReport")]
public partial class ImageInReport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "InReport",
            table: "DocumentOccurrences",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.DropColumn(
            name: "PreparationRole",
            table: "DocumentOccurrences");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InReport",
            table: "DocumentOccurrences");

        migrationBuilder.AddColumn<string>(
            name: "PreparationRole",
            table: "DocumentOccurrences",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);
    }
}
