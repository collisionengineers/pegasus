using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainedMailFolderMoves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetainedMailFolderMoves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetainedMailboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    MailboxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ImmutableMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceFolderId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DestinationFolderId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FolderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedMailFolderMoves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetainedMailFolderMoves_RetainedMailboxMessages_RetainedMailboxMessageId",
                        column: x => x.RetainedMailboxMessageId,
                        principalTable: "RetainedMailboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailFolderMoves_OperationKey",
                table: "RetainedMailFolderMoves",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailFolderMoves_RetainedMailboxMessageId_RecordedAtUtc",
                table: "RetainedMailFolderMoves",
                columns: new[] { "RetainedMailboxMessageId", "RecordedAtUtc" });

            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[RetainedMailFolderMoves] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[RetainedMailFolderMoves] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                    DENY DELETE ON OBJECT::[dbo].[RetainedMailFolderMoves] TO [pegasus_worker_runtime_role];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetainedMailFolderMoves");
        }
    }
}
