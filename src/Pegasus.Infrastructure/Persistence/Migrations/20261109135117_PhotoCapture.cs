using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhotoCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeclaredRegistration",
                table: "IntakeSubmissionGroups",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StandingPhotoLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ForwardedMode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LiveCameraOnly = table.Column<bool>(type: "bit", nullable: false),
                    LinkLifetimeDays = table.Column<int>(type: "int", nullable: false),
                    OriginalsInBackground = table.Column<bool>(type: "bit", nullable: false),
                    PausedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RetiredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingPhotoLinks", x => x.Id);
                    table.CheckConstraint("CK_StandingPhotoLinks_ForwardedMode", "[ForwardedMode] IN ('guided', 'quick')");
                    table.CheckConstraint("CK_StandingPhotoLinks_Kind", "[Kind] IN ('contact', 'website')");
                    table.CheckConstraint("CK_StandingPhotoLinks_Organization", "([Kind] = 'contact' AND [OrganizationId] IS NOT NULL) OR ([Kind] = 'website' AND [OrganizationId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_StandingPhotoLinks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhotoCaptures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StandingLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SubmissionGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RoutingReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NormalizedRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Mode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    LiveCameraOnly = table.Column<bool>(type: "bit", nullable: false),
                    OriginalsInBackground = table.Column<bool>(type: "bit", nullable: false),
                    IssuedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FirstItemAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastItemAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SettledBatches = table.Column<int>(type: "int", nullable: false),
                    AcceptedItemCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedBytes = table.Column<long>(type: "bigint", nullable: false),
                    PhotoCount = table.Column<int>(type: "int", nullable: false),
                    OriginalCount = table.Column<int>(type: "int", nullable: false),
                    DocumentCount = table.Column<int>(type: "int", nullable: false),
                    VideoCount = table.Column<int>(type: "int", nullable: false),
                    ReminderCount = table.Column<int>(type: "int", nullable: false),
                    LastRemindedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CallLoggedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoCaptures", x => x.Id);
                    table.CheckConstraint("CK_PhotoCaptures_Destination", "[Destination] IN ('undecided', 'case', 'vehicle_images')");
                    table.CheckConstraint("CK_PhotoCaptures_Mode", "[Mode] IN ('guided', 'quick')");
                    table.CheckConstraint("CK_PhotoCaptures_Origin", "[Origin] IN ('case_link', 'standing_staff', 'standing_forwarded', 'website')");
                    table.ForeignKey(
                        name: "FK_PhotoCaptures_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotoCaptures_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotoCaptures_StandingPhotoLinks_StandingLinkId",
                        column: x => x.StandingLinkId,
                        principalTable: "StandingPhotoLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhotoCaptureItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaptureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Batch = table.Column<int>(type: "int", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    StoryGroup = table.Column<int>(type: "int", nullable: false),
                    StorySequence = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TakenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LiveCamera = table.Column<bool>(type: "bit", nullable: false),
                    InReportOnArrival = table.Column<bool>(type: "bit", nullable: false),
                    ContentHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OriginalReceived = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoCaptureItems", x => x.Id);
                    table.CheckConstraint("CK_PhotoCaptureItems_Kind", "[Kind] IN ('overview', 'damage', 'number_plate', 'vin', 'dashboard', 'scene', 'other_vehicle', 'paperwork', 'video')");
                    table.CheckConstraint("CK_PhotoCaptureItems_Stage", "[Stage] IS NULL OR [Stage] IN ('overview', 'closer', 'close_up', 'extra')");
                    table.ForeignKey(
                        name: "FK_PhotoCaptureItems_PhotoCaptures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "PhotoCaptures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhotoCaptureTokens",
                columns: table => new
                {
                    TokenDigest = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    CaptureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IssuedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoCaptureTokens", x => x.TokenDigest);
                    table.CheckConstraint("CK_PhotoCaptureTokens_Purpose", "[Purpose] IN ('email', 'copy', 'handoff', 'session')");
                    table.ForeignKey(
                        name: "FK_PhotoCaptureTokens_PhotoCaptures_CaptureId",
                        column: x => x.CaptureId,
                        principalTable: "PhotoCaptures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Two existing check constraints learn one value each: the photo
            // link's source kind and the Photos received bell cause (FRD-28).
            // Widening a constraint drops nothing and rewrites no row, so the
            // migration stays additive (R605); it is written as SQL because a
            // DropCheckConstraint operation would read as non-additive.
            migrationBuilder.Sql("""
                ALTER TABLE [dbo].[StaffNotifications] DROP CONSTRAINT [CK_StaffNotifications_Cause];
                ALTER TABLE [dbo].[StaffNotifications] WITH CHECK ADD CONSTRAINT [CK_StaffNotifications_Cause]
                    CHECK ([Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived', 'CancellationReceived', 'QueryAssigned', 'PhotosReceived'));
                ALTER TABLE [dbo].[CaseDataFields] DROP CONSTRAINT [CK_CaseDataFields_SourceKind];
                ALTER TABLE [dbo].[CaseDataFields] WITH CHECK ADD CONSTRAINT [CK_CaseDataFields_SourceKind]
                    CHECK ([SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'principal_setting', 'principal_api', 'photo_link'));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureItems_CaptureId_Batch_Ordinal",
                table: "PhotoCaptureItems",
                columns: new[] { "CaptureId", "Batch", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureItems_CaptureId_ClientItemId",
                table: "PhotoCaptureItems",
                columns: new[] { "CaptureId", "ClientItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureItems_IntakeAssetId",
                table: "PhotoCaptureItems",
                column: "IntakeAssetId",
                filter: "[IntakeAssetId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureItems_StagedReceiptId",
                table: "PhotoCaptureItems",
                column: "StagedReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptures_CaseId",
                table: "PhotoCaptures",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptures_OrganizationId",
                table: "PhotoCaptures",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptures_Origin_CreatedAtUtc",
                table: "PhotoCaptures",
                columns: new[] { "Origin", "CreatedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptures_SettledAtUtc_SentAtUtc_ExpiresAtUtc",
                table: "PhotoCaptures",
                columns: new[] { "SettledAtUtc", "SentAtUtc", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptures_StandingLinkId_CreatedAtUtc",
                table: "PhotoCaptures",
                columns: new[] { "StandingLinkId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptures_SubmissionGroupId",
                table: "PhotoCaptures",
                column: "SubmissionGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoCaptureTokens_CaptureId",
                table: "PhotoCaptureTokens",
                column: "CaptureId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingPhotoLinks_Kind",
                table: "StandingPhotoLinks",
                column: "Kind",
                unique: true,
                filter: "[Kind] = 'website' AND [RetiredAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StandingPhotoLinks_OrganizationId",
                table: "StandingPhotoLinks",
                column: "OrganizationId",
                unique: true,
                filter: "[OrganizationId] IS NOT NULL AND [RetiredAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StandingPhotoLinks_Slug",
                table: "StandingPhotoLinks",
                column: "Slug",
                unique: true);

            // The runtime roles' grants on the new tables (RuntimePermissions.csv):
            // the Web reads, inserts and updates them, the Worker reads them and
            // updates the captures and their items as they settle; neither deletes.
            migrationBuilder.Sql("""
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptures] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PhotoCaptures] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptures] TO [pegasus_worker_runtime_role];
                GRANT SELECT, UPDATE ON OBJECT::[dbo].[PhotoCaptures] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptureTokens] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PhotoCaptureTokens] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptureTokens] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[PhotoCaptureTokens] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptureItems] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PhotoCaptureItems] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[PhotoCaptureItems] TO [pegasus_worker_runtime_role];
                GRANT SELECT, UPDATE ON OBJECT::[dbo].[PhotoCaptureItems] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[StandingPhotoLinks] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[StandingPhotoLinks] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[StandingPhotoLinks] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[StandingPhotoLinks] TO [pegasus_worker_runtime_role];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotoCaptureItems");

            migrationBuilder.DropTable(
                name: "PhotoCaptureTokens");

            migrationBuilder.DropTable(
                name: "PhotoCaptures");

            migrationBuilder.DropTable(
                name: "StandingPhotoLinks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffNotifications_Cause",
                table: "StaffNotifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields");

            migrationBuilder.DropColumn(
                name: "DeclaredRegistration",
                table: "IntakeSubmissionGroups");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffNotifications_Cause",
                table: "StaffNotifications",
                sql: "[Cause] IN ('AiDraftReady', 'CaseAssigned', 'EditedByOther', 'EmailReceived', 'QueryReceived', 'CancellationReceived', 'QueryAssigned')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDataFields_SourceKind",
                table: "CaseDataFields",
                sql: "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup', 'principal_setting', 'principal_api')");
        }
    }
}
