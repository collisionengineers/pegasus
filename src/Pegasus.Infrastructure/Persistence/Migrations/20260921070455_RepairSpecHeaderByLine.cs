using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// v28 P32 and P48 (ruled 20 September 2026): a repair specification's
    /// header carries no notes, repair days or materials figure. The header's
    /// materials move onto the first paint line (or the first line) so no
    /// costed figure is lost; the regional uplift flag (P17) joins the header.
    /// Forward only for the notes and days.
    /// </summary>
    public partial class RepairSpecHeaderByLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "PaintMaterials",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "RepairDays",
                table: "CaseRepairSpecifications");

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
            migrationBuilder.DropColumn(
                name: "RegionalUplift",
                table: "CaseRepairSpecifications");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "CaseRepairSpecifications",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PaintMaterials",
                table: "CaseRepairSpecifications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepairDays",
                table: "CaseRepairSpecifications",
                type: "int",
                nullable: true);
        }
    }
}
