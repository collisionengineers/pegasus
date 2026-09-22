using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Whether a report image prints on a page of its own (v28 P41, ruled 20
    /// September 2026). A flag on an image the report uses; an image not in
    /// the report never carries it. The Web runtime saves and resets preparation
    /// values on the occurrence, so it also needs UPDATE on that table.
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
            migrationBuilder.Sql("GRANT UPDATE ON OBJECT::[dbo].[DocumentOccurrences] TO [pegasus_web_runtime_role];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("REVOKE UPDATE ON OBJECT::[dbo].[DocumentOccurrences] FROM [pegasus_web_runtime_role];");
            migrationBuilder.DropColumn(
                name: "PreparationFullPage",
                table: "DocumentOccurrences");
        }
    }
}
