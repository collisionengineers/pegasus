using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageIntakeSubmissionGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionGroupId",
                table: "ImageIntakes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_SubmissionGroupId",
                table: "ImageIntakes",
                column: "SubmissionGroupId",
                unique: true,
                filter: "[SubmissionGroupId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ImageIntakes_IntakeSubmissionGroups_SubmissionGroupId",
                table: "ImageIntakes",
                column: "SubmissionGroupId",
                principalTable: "IntakeSubmissionGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImageIntakes_IntakeSubmissionGroups_SubmissionGroupId",
                table: "ImageIntakes");

            migrationBuilder.DropIndex(
                name: "IX_ImageIntakes_SubmissionGroupId",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "SubmissionGroupId",
                table: "ImageIntakes");
        }
    }
}
