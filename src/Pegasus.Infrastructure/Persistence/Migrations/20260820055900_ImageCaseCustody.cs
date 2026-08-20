using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageCaseCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustodyConfirmedAtUtc",
                table: "ImageIntakes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CustodyMergedAtUtc",
                table: "ImageIntakes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustodyRootRemoteId",
                table: "ImageIntakes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustodyState",
                table: "ImageIntakes",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CaseId",
                table: "ExternalWorkItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ImageIntakeId",
                table: "ExternalWorkItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalWorkItems_ImageIntakeId",
                table: "ExternalWorkItems",
                column: "ImageIntakeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalWorkItems_ImageIntakes_ImageIntakeId",
                table: "ExternalWorkItems",
                column: "ImageIntakeId",
                principalTable: "ImageIntakes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExternalWorkItems_ImageIntakes_ImageIntakeId",
                table: "ExternalWorkItems");

            migrationBuilder.DropIndex(
                name: "IX_ExternalWorkItems_ImageIntakeId",
                table: "ExternalWorkItems");

            migrationBuilder.DropColumn(
                name: "CustodyConfirmedAtUtc",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "CustodyMergedAtUtc",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "CustodyRootRemoteId",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "CustodyState",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "ImageIntakeId",
                table: "ExternalWorkItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "CaseId",
                table: "ExternalWorkItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
