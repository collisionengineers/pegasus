using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BoxDocumentSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeriesIdentity",
                table: "CaseDocuments",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_CaseId_SeriesIdentity",
                table: "CaseDocuments",
                columns: new[] { "CaseId", "SeriesIdentity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseDocuments_CaseId_SeriesIdentity",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "SeriesIdentity",
                table: "CaseDocuments");
        }
    }
}
