using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReleaseNoteCreateIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "ReleaseNotes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "ReleaseNotes",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseNotes_OperationKey",
                table: "ReleaseNotes",
                column: "OperationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReleaseNotes_OperationKey",
                table: "ReleaseNotes");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "ReleaseNotes");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "ReleaseNotes");
        }
    }
}
