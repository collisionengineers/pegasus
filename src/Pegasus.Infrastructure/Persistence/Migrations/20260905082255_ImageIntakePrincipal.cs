using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageIntakePrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrincipalId",
                table: "ImageIntakes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_PrincipalId",
                table: "ImageIntakes",
                column: "PrincipalId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImageIntakes_Principals_PrincipalId",
                table: "ImageIntakes",
                column: "PrincipalId",
                principalTable: "Principals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImageIntakes_Principals_PrincipalId",
                table: "ImageIntakes");

            migrationBuilder.DropIndex(
                name: "IX_ImageIntakes_PrincipalId",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "PrincipalId",
                table: "ImageIntakes");
        }
    }
}
