using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StaffAccountSignOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultSignOffEngineer",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSignOffEngineer",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SignOffPrintedName",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignOffQualifications",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SignOffSignature",
                table: "AspNetUsers",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignOffSignatureDigest",
                table: "AspNetUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsDefaultSignOffEngineer",
                table: "AspNetUsers",
                column: "IsDefaultSignOffEngineer",
                unique: true,
                filter: "[IsDefaultSignOffEngineer] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsDefaultSignOffEngineer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsDefaultSignOffEngineer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsSignOffEngineer",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SignOffPrintedName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SignOffQualifications",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SignOffSignature",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SignOffSignatureDigest",
                table: "AspNetUsers");
        }
    }
}
