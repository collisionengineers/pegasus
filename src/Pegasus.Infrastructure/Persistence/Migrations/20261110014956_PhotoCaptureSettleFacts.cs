using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhotoCaptureSettleFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhotoLinkDefaultLifetimeDays",
                table: "WorkflowConfigurations",
                type: "int",
                nullable: false,
                defaultValue: 7);

            migrationBuilder.AddColumn<string>(
                name: "PhotoLinkLifetimesOffered",
                table: "WorkflowConfigurations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "3,7,14");

            migrationBuilder.AddColumn<bool>(
                name: "PhotoLinkStartLiveCameraOnly",
                table: "WorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoLinkStartMode",
                table: "WorkflowConfigurations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "guided");

            migrationBuilder.AddColumn<bool>(
                name: "PhotoLinkStartOriginals",
                table: "WorkflowConfigurations",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImagesCompletedAtUtc",
                table: "PhotoCaptures",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuickLinkFor",
                table: "PhotoCaptures",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeclaredCaseId",
                table: "PhotoCaptureItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeclaredIncidentDate",
                table: "IntakeSubmissionGroups",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoCaptureBatches",
                columns: table => new
                {
                    CaptureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Batch = table.Column<int>(type: "int", nullable: false),
                    SubmissionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReachedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BellOwedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoCaptureBatches", x => new { x.CaptureId, x.Batch });
                    table.ForeignKey(
                        name: "FK_PhotoCaptureBatches_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotoCaptureBatches_PhotoCaptures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "PhotoCaptures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StandingPhotoLinkAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StandingPhotoLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingPhotoLinkAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandingPhotoLinkAddresses_StandingPhotoLinks_StandingPhotoLinkId",
                        column: x => x.StandingPhotoLinkId,
                        principalTable: "StandingPhotoLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "WorkflowConfigurations",
                keyColumn: "Id",
                keyValue: "case-workflow",
                columns: new[] { "PhotoLinkDefaultLifetimeDays", "PhotoLinkLifetimesOffered", "PhotoLinkStartLiveCameraOnly", "PhotoLinkStartMode", "PhotoLinkStartOriginals" },
                values: new object[] { 7, "3,7,14", true, "guided", true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowConfigurations_PhotoLinkDefaultLifetimeDays",
                table: "WorkflowConfigurations",
                sql: "[PhotoLinkDefaultLifetimeDays] IN (3, 7, 14)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkflowConfigurations_PhotoLinkStartMode",
                table: "WorkflowConfigurations",
                sql: "[PhotoLinkStartMode] IN ('guided', 'quick')");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureBatches_BellOwedAtUtc",
                table: "PhotoCaptureBatches",
                column: "BellOwedAtUtc",
                filter: "[BellOwedAtUtc] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureBatches_CaseId",
                table: "PhotoCaptureBatches",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureBatches_SubmissionGroupId",
                table: "PhotoCaptureBatches",
                column: "SubmissionGroupId",
                unique: true,
                filter: "[SubmissionGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StandingPhotoLinkAddresses_StandingPhotoLinkId",
                table: "StandingPhotoLinkAddresses",
                column: "StandingPhotoLinkId");

            // The runtime roles' grants on the new tables (RuntimePermissions.csv):
            // the Web reads, adds, edits and removes a standing link's own saved
            // addresses and the Worker never deletes them; the Worker alone writes a
            // capture's batches as it settles them, and neither role deletes one.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[StandingPhotoLinkAddresses] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[StandingPhotoLinkAddresses] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptureBatches] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptureBatches] TO [pegasus_worker_runtime_role];
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PhotoCaptureBatches] TO [pegasus_worker_runtime_role];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoCaptureBatches");

            migrationBuilder.DropTable(
                name: "StandingPhotoLinkAddresses");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowConfigurations_PhotoLinkDefaultLifetimeDays",
                table: "WorkflowConfigurations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkflowConfigurations_PhotoLinkStartMode",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "PhotoLinkDefaultLifetimeDays",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "PhotoLinkLifetimesOffered",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "PhotoLinkStartLiveCameraOnly",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "PhotoLinkStartMode",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "PhotoLinkStartOriginals",
                table: "WorkflowConfigurations");

            migrationBuilder.DropColumn(
                name: "ImagesCompletedAtUtc",
                table: "PhotoCaptures");

            migrationBuilder.DropColumn(
                name: "QuickLinkFor",
                table: "PhotoCaptures");

            migrationBuilder.DropColumn(
                name: "DeclaredCaseId",
                table: "PhotoCaptureItems");

            migrationBuilder.DropColumn(
                name: "DeclaredIncidentDate",
                table: "IntakeSubmissionGroups");
        }
    }
}
