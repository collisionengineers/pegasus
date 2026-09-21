using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Whether a report image prints on a page of its own (v28 P41, ruled 20
    /// September 2026). A flag on an image the report uses; an image not in
    /// the report never carries it. Additive.
    /// </summary>
    public partial class ReportImageFullPage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PreparationFullPage",
                table: "DocumentOccurrences",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreparationFullPage",
                table: "DocumentOccurrences");
        }
    }
}
