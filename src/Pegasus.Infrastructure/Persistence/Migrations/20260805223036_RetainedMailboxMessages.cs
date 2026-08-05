using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainedMailboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetainedMailboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MailboxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MailboxAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FolderScope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FolderIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImmutableMessageId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConversationIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InternetMessageIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SenderAddress = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    SenderDisplayName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    ToAddressesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcAddressesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BodyExcerpt = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BodyPlainText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    SourceLength = table.Column<long>(type: "bigint", nullable: false),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RetainedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedMailboxMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
                        column: x => x.MailboxId,
                        principalTable: "ApprovedInboxPollStates",
                        principalColumn: "MailboxId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetainedMailboxAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetainedMailboxMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetainedMailboxAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetainedMailboxAttachments_RetainedMailboxMessages_RetainedMailboxMessageId",
                        column: x => x.RetainedMailboxMessageId,
                        principalTable: "RetainedMailboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxAttachments_RetainedMailboxMessageId_Ordinal",
                table: "RetainedMailboxAttachments",
                columns: new[] { "RetainedMailboxMessageId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_ConversationIdentity",
                table: "RetainedMailboxMessages",
                column: "ConversationIdentity");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_ExternalReceiptToken",
                table: "RetainedMailboxMessages",
                column: "ExternalReceiptToken");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "FolderScope", "ReceivedAtUtc", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "ImmutableMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages",
                columns: new[] { "ReceivedAtUtc", "Id" },
                descending: new[] { true, false });

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.database_principals
                        WHERE name = N'pegasus_web_runtime_role'
                          AND [type] = 'R'
                          AND is_fixed_role = 0
                          AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                        THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.database_principals
                        WHERE name = N'pegasus_worker_runtime_role'
                          AND [type] = 'R'
                          AND is_fixed_role = 0
                          AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                        THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
                    """);
                // Web gets SELECT and nothing else. The mail workspace is a viewer:
                // it never marks a message read, never moves one, and never writes a
                // retained row — and the database says so rather than the page being
                // trusted to behave.
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[RetainedMailboxMessages] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[RetainedMailboxAttachments] TO [pegasus_web_runtime_role];");
                // The Worker inserts, and only inserts: a retained row records what
                // arrived, so a redelivery is refused by the unique index rather
                // than overwriting the original.
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[RetainedMailboxMessages] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[RetainedMailboxAttachments] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetainedMailboxAttachments");

            migrationBuilder.DropTable(
                name: "RetainedMailboxMessages");
        }
    }
}
