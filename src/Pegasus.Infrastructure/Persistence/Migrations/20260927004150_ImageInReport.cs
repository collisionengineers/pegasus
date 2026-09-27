using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The image tag decides how an image in the report prints, and whether the
/// report uses an image is a plain on/off (operator, 26 September 2026;
/// FRD-06). Additive: each occurrence gains the InReport flag, and a new image
/// is in the report. An existing image keeps a choice staff made: a stored
/// Close-up, Overview or Supporting role is in, a stored Not used is out. An
/// image with no stored role was never touched, so it is in the report like a
/// new image, unless it is tagged Third party or Reflection. The retired role
/// column stays, unread.
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

        // The two tag identifiers are ImageTagVocabulary.ThirdPartyId and
        // ReflectionId, fixed since 20260910120000_CaseImageTags seeded them.
        migrationBuilder.Sql(
            """
            UPDATE occurrence
            SET InReport = CASE
                WHEN occurrence.PreparationRole IN (N'CloseUp', N'Overview', N'Supporting') THEN 1
                WHEN occurrence.PreparationRole IS NOT NULL THEN 0
                WHEN EXISTS (
                    SELECT 1
                    FROM DocumentOccurrenceTags AS tag
                    WHERE tag.OccurrenceId = occurrence.Id
                      AND tag.TagId IN (
                          '00000000-0000-4000-8000-0000000017a3',
                          '00000000-0000-4000-8000-0000000017a4')) THEN 0
                ELSE 1
            END
            FROM DocumentOccurrences AS occurrence
            WHERE occurrence.SemanticRole = N'Image';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InReport",
            table: "DocumentOccurrences");
    }
}
