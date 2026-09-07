using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicUploadOccurrenceReplacementLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplacesOccurrenceId",
                table: "PublicUploadOccurrences",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PublicUploadOccurrences_SessionId_Id",
                table: "PublicUploadOccurrences",
                columns: new[] { "SessionId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId",
                table: "PublicUploadOccurrences",
                columns: new[] { "SessionId", "ReplacesOccurrenceId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PublicUploadOccurrences_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId",
                table: "PublicUploadOccurrences",
                columns: new[] { "SessionId", "ReplacesOccurrenceId" },
                principalTable: "PublicUploadOccurrences",
                principalColumns: new[] { "SessionId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PublicUploadOccurrences_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId",
                table: "PublicUploadOccurrences");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PublicUploadOccurrences_SessionId_Id",
                table: "PublicUploadOccurrences");

            migrationBuilder.DropIndex(
                name: "IX_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId",
                table: "PublicUploadOccurrences");

            migrationBuilder.DropColumn(
                name: "ReplacesOccurrenceId",
                table: "PublicUploadOccurrences");
        }
    }
}
