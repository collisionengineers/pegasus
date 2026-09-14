using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Pre-Case image crop and tag (Phase 6c, v26 13 September): an image on an
/// image-initiated Case, a Triage or an Unidentified item carries a stored crop
/// rectangle, rotation and tags, mirroring the Case image's own
/// (<c>DocumentOccurrences</c> preparation columns and <c>DocumentOccurrenceTags</c>).
/// Keyed by the retained intake asset; no row means an unprepared image.
///
/// Web records crops (select, insert, update; never delete) and tags (select,
/// insert, delete; never update) from the pre-Case records. The Worker reads
/// both when intake custody records the image as a Case document and copies the
/// tags onto the occurrence, so it gains INSERT on <c>DocumentOccurrenceTags</c>.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260914091000_PreCaseImagePreparation")]
public partial class PreCaseImagePreparation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.CreateTable(
            name: "IntakeAssetPreparations",
            columns: table => new
            {
                IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RotationDegrees = table.Column<short>(type: "smallint", nullable: false),
                CropLeft = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                CropTop = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                CropWidth = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                CropHeight = table.Column<decimal>(type: "decimal(8,7)", precision: 8, scale: 7, nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false),
                PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                PreparedByKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                PreparedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeAssetPreparations", x => x.IntakeAssetId);
                table.CheckConstraint("CK_IntakeAssetPreparations_Crop", "([CropLeft] IS NULL AND [CropTop] IS NULL AND [CropWidth] IS NULL AND [CropHeight] IS NULL) OR ([CropLeft] BETWEEN 0 AND 1 AND [CropTop] BETWEEN 0 AND 1 AND [CropWidth] > 0 AND [CropWidth] <= 1 AND [CropHeight] > 0 AND [CropHeight] <= 1 AND [CropLeft] + [CropWidth] <= 1 AND [CropTop] + [CropHeight] <= 1)");
                table.CheckConstraint("CK_IntakeAssetPreparations_Rotation", "[RotationDegrees] IN (0, 90, 180, 270)");
                table.ForeignKey(
                    name: "FK_IntakeAssetPreparations_IntakeAssets_IntakeAssetId",
                    column: x => x.IntakeAssetId,
                    principalTable: "IntakeAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "IntakeAssetTags",
            columns: table => new
            {
                IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TagId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AppliedByKind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                AppliedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                AppliedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeAssetTags", x => new { x.IntakeAssetId, x.TagId });
                table.ForeignKey(
                    name: "FK_IntakeAssetTags_ImageTags_TagId",
                    column: x => x.TagId,
                    principalTable: "ImageTags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_IntakeAssetTags_IntakeAssets_IntakeAssetId",
                    column: x => x.IntakeAssetId,
                    principalTable: "IntakeAssets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeAssetTags_TagId",
            table: "IntakeAssetTags",
            column: "TagId");

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_web_runtime_role];
                DENY UPDATE ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_worker_runtime_role];
                DENY UPDATE, DELETE ON OBJECT::[dbo].[IntakeAssetPreparations] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_worker_runtime_role];
                DENY UPDATE, DELETE ON OBJECT::[dbo].[IntakeAssetTags] TO [pegasus_worker_runtime_role];
                GRANT INSERT ON OBJECT::[dbo].[DocumentOccurrenceTags] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                REVOKE INSERT ON OBJECT::[dbo].[DocumentOccurrenceTags] FROM [pegasus_worker_runtime_role];
            """);
        migrationBuilder.DropTable(name: "IntakeAssetPreparations");
        migrationBuilder.DropTable(name: "IntakeAssetTags");
    }
}
