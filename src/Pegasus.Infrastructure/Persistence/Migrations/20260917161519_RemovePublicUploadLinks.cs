using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemovePublicUploadLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseDueChasers_RequestUploadLinks_RequestLinkReference",
                table: "CaseDueChasers");

            migrationBuilder.DropTable(
                name: "PublicUploadOccurrences");

            migrationBuilder.DropTable(
                name: "RequestUploadReceipts");

            migrationBuilder.DropTable(
                name: "PublicUploadSessions");

            migrationBuilder.DropTable(
                name: "RequestUploadLinks");

            migrationBuilder.DropIndex(
                name: "IX_CaseDueChasers_RequestLinkReference",
                table: "CaseDueChasers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseDueChasers_RequestLink",
                table: "CaseDueChasers");

            migrationBuilder.DropColumn(
                name: "RequestLinkPurpose",
                table: "CaseDueChasers");

            migrationBuilder.DropColumn(
                name: "RequestLinkReference",
                table: "CaseDueChasers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestLinkPurpose",
                table: "CaseDueChasers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestLinkReference",
                table: "CaseDueChasers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RequestUploadLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedByteCount = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedFileCount = table.Column<int>(type: "int", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LimitsVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokeOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TokenDigest = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestUploadLinks", x => x.Id);
                    table.CheckConstraint("CK_RequestUploadLinks_AcceptedByteCount", "[AcceptedByteCount] >= 0");
                    table.CheckConstraint("CK_RequestUploadLinks_AcceptedFileCount", "[AcceptedFileCount] >= 0");
                    table.ForeignKey(
                        name: "FK_RequestUploadLinks_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicUploadSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinalizedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LimitsVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestUploadLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUploadSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicUploadSessions_RequestUploadLinks_RequestUploadLinkId",
                        column: x => x.RequestUploadLinkId,
                        principalTable: "RequestUploadLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RequestUploadReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestUploadReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestUploadReceipts_DocumentOccurrences_OccurrenceId",
                        column: x => x.OccurrenceId,
                        principalTable: "DocumentOccurrences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestUploadReceipts_DocumentVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "DocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequestUploadReceipts_RequestUploadLinks_RequestId",
                        column: x => x.RequestId,
                        principalTable: "RequestUploadLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PublicUploadOccurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustodyState = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MediaType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProposedName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReplacesOccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicUploadOccurrences", x => x.Id);
                    table.UniqueConstraint("AK_PublicUploadOccurrences_SessionId_Id", x => new { x.SessionId, x.Id });
                    table.ForeignKey(
                        name: "FK_PublicUploadOccurrences_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId",
                        columns: x => new { x.SessionId, x.ReplacesOccurrenceId },
                        principalTable: "PublicUploadOccurrences",
                        principalColumns: new[] { "SessionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicUploadOccurrences_PublicUploadSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PublicUploadSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDueChasers_RequestLinkReference",
                table: "CaseDueChasers",
                column: "RequestLinkReference");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseDueChasers_RequestLink",
                table: "CaseDueChasers",
                sql: "([RequestLinkReference] IS NULL AND [RequestLinkPurpose] IS NULL) OR ([RequestLinkReference] IS NOT NULL AND [RequestLinkPurpose] = 'missing-material-upload')");

            migrationBuilder.CreateIndex(
                name: "IX_PublicUploadOccurrences_SessionId_OperationKey",
                table: "PublicUploadOccurrences",
                columns: new[] { "SessionId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId",
                table: "PublicUploadOccurrences",
                columns: new[] { "SessionId", "ReplacesOccurrenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PublicUploadSessions_RequestUploadLinkId",
                table: "PublicUploadSessions",
                column: "RequestUploadLinkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadLinks_CaseId_CreateOperationKey",
                table: "RequestUploadLinks",
                columns: new[] { "CaseId", "CreateOperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadLinks_CreatedAtUtc_Id",
                table: "RequestUploadLinks",
                columns: new[] { "CreatedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadLinks_RevokedAtUtc_Id",
                table: "RequestUploadLinks",
                columns: new[] { "RevokedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadLinks_TokenDigest",
                table: "RequestUploadLinks",
                column: "TokenDigest",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_OccurrenceId",
                table: "RequestUploadReceipts",
                column: "OccurrenceId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_RequestId_OperationKey",
                table: "RequestUploadReceipts",
                columns: new[] { "RequestId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_RequestId_ReceivedAtUtc",
                table: "RequestUploadReceipts",
                columns: new[] { "RequestId", "ReceivedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_RequestUploadReceipts_VersionId",
                table: "RequestUploadReceipts",
                column: "VersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseDueChasers_RequestUploadLinks_RequestLinkReference",
                table: "CaseDueChasers",
                column: "RequestLinkReference",
                principalTable: "RequestUploadLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
