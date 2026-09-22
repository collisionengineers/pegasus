using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// v28 P32 and P48 (ruled 20 September 2026): a repair specification's
    /// header carries no notes, repair days or materials figure. The header's
    /// materials move onto the first paint line (or the first line). A
    /// materials-only Draft receives one paint line. The obsolete header
    /// columns are removed; this migration is forward only.
    /// </summary>
    public partial class RepairSpecHeaderByLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO CaseEstimateLines
                    (Id, CaseId, RepairSpecificationId, Position, LineType,
                     Description, Materials, Unpriced, RecordedByKind, RecordedBy, RecordedAtUtc)
                SELECT NEWID(), s.CaseId, s.Id, 1, 'paint_prep',
                       N'Paint materials', 0, 0, 'Automation', 'migration', SYSUTCDATETIME()
                FROM CaseRepairSpecifications s
                WHERE s.PaintMaterials IS NOT NULL AND s.PaintMaterials <> 0
                  AND NOT EXISTS (
                      SELECT 1 FROM CaseEstimateLines l
                      WHERE l.RepairSpecificationId = s.Id);

                UPDATE l
                SET Materials = ISNULL(l.Materials, 0) + s.PaintMaterials
                FROM CaseEstimateLines l
                JOIN CaseRepairSpecifications s ON s.Id = l.RepairSpecificationId
                WHERE s.PaintMaterials IS NOT NULL AND s.PaintMaterials <> 0
                  AND l.Id = (
                      SELECT TOP 1 x.Id
                      FROM CaseEstimateLines x
                      WHERE x.RepairSpecificationId = s.Id
                      ORDER BY CASE WHEN x.LineType IN ('paint_new', 'paint_repair', 'paint_blend', 'paint_prep') THEN 0 ELSE 1 END, x.Position);
                """);

            migrationBuilder.DropColumn(name: "PaintMaterials", table: "CaseRepairSpecifications");
            migrationBuilder.DropColumn(name: "RepairDays", table: "CaseRepairSpecifications");
            migrationBuilder.DropColumn(name: "Notes", table: "CaseRepairSpecifications");

            migrationBuilder.AddColumn<bool>(
                name: "RegionalUplift",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Repair specification header removal is forward only; restore from an approved backup instead.");
        }
    }
}
