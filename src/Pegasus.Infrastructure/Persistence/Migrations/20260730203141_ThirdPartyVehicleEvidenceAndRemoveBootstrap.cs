using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThirdPartyVehicleEvidenceAndRemoveBootstrap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationInitializations");

            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyVehicleConfirmationOperationKey",
                table: "DocumentOccurrences",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThirdPartyVehicleConfirmationReason",
                table: "DocumentOccurrences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ThirdPartyVehicleConfirmedAtUtc",
                table: "DocumentOccurrences",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentOccurrences_CaseId_ThirdPartyVehicleConfirmedAtUtc",
                table: "DocumentOccurrences",
                columns: new[] { "CaseId", "ThirdPartyVehicleConfirmedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DocumentOccurrences_CaseId_ThirdPartyVehicleConfirmedAtUtc",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "ThirdPartyVehicleConfirmationOperationKey",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "ThirdPartyVehicleConfirmationReason",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "ThirdPartyVehicleConfirmedAtUtc",
                table: "DocumentOccurrences");

            migrationBuilder.CreateTable(
                name: "ApplicationInitializations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ManifestSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    MigrationId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TargetIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationInitializations", x => x.Id);
                });
        }
    }
}
