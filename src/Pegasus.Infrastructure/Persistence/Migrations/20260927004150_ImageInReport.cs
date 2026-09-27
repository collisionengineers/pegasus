using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The image tag decides how an image in the report prints, and whether the
/// report uses an image is a plain on/off (operator, 26 September 2026;
/// FRD-06). Additive: each occurrence gains the InReport flag, and a new image
/// is in the report. Every existing image keeps whether the report used it:
/// any role but Not used becomes on. The retired role column stays, unread.
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

        migrationBuilder.Sql(
            """
            UPDATE DocumentOccurrences
            SET InReport = CASE
                WHEN PreparationRole IN (N'CloseUp', N'Overview', N'Supporting') THEN 1
                ELSE 0
            END
            WHERE SemanticRole = N'Image';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InReport",
            table: "DocumentOccurrences");
    }
}
