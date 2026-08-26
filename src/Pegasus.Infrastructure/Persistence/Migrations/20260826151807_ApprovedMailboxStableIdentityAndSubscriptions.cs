using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedMailboxStableIdentityAndSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_MailboxId",
                table: "ApprovedInboxPoisonMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovedInboxPollStates",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropIndex(
                name: "IX_ApprovedInboxPollStates_DueAtUtc_MailboxId",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropIndex(
                name: "IX_ApprovedInboxPoisonMessages_MailboxId_OccurrenceKey",
                table: "ApprovedInboxPoisonMessages");

            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages");

            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [dbo].[ApprovedInboxPollStates] state " +
                "LEFT JOIN [dbo].[ApprovedMailboxes] mailbox ON mailbox.[MailboxIdentity] = state.[MailboxId] " +
                "WHERE mailbox.[Id] IS NULL) THROW 51000, 'An approved Inbox poll state has no exact approved mailbox identity.', 1;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [dbo].[ApprovedInboxPoisonMessages] poison " +
                "LEFT JOIN [dbo].[ApprovedMailboxes] mailbox ON mailbox.[MailboxIdentity] = poison.[MailboxId] " +
                "WHERE mailbox.[Id] IS NULL) THROW 51000, 'An approved Inbox poison message has no exact approved mailbox identity.', 1;");
            migrationBuilder.Sql(
                "IF EXISTS (SELECT 1 FROM [dbo].[RetainedMailboxMessages] retained " +
                "LEFT JOIN [dbo].[ApprovedMailboxes] mailbox ON mailbox.[MailboxIdentity] = retained.[MailboxId] " +
                "WHERE mailbox.[Id] IS NULL) THROW 51000, 'A retained mailbox message has no exact approved mailbox identity.', 1;");
            migrationBuilder.Sql(
                "UPDATE state SET [MailboxId] = CONVERT(nvarchar(36), mailbox.[Id]) " +
                "FROM [dbo].[ApprovedInboxPollStates] state " +
                "JOIN [dbo].[ApprovedMailboxes] mailbox ON mailbox.[MailboxIdentity] = state.[MailboxId];");
            migrationBuilder.Sql(
                "UPDATE poison SET [MailboxId] = CONVERT(nvarchar(36), mailbox.[Id]) " +
                "FROM [dbo].[ApprovedInboxPoisonMessages] poison " +
                "JOIN [dbo].[ApprovedMailboxes] mailbox ON mailbox.[MailboxIdentity] = poison.[MailboxId];");
            migrationBuilder.Sql(
                "UPDATE retained SET [MailboxId] = CONVERT(nvarchar(36), mailbox.[Id]) " +
                "FROM [dbo].[RetainedMailboxMessages] retained " +
                "JOIN [dbo].[ApprovedMailboxes] mailbox ON mailbox.[MailboxIdentity] = retained.[MailboxId];");

            migrationBuilder.AlterColumn<Guid>(
                name: "MailboxId",
                table: "ApprovedInboxPollStates",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "MailboxId",
                table: "ApprovedInboxPoisonMessages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "MailboxId",
                table: "RetainedMailboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.RenameColumn(
                name: "MailboxId",
                table: "ApprovedInboxPollStates",
                newName: "ApprovedMailboxId");

            migrationBuilder.RenameColumn(
                name: "MailboxId",
                table: "ApprovedInboxPoisonMessages",
                newName: "ApprovedMailboxId");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAtUtc",
                table: "ApprovedMailboxes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAtUtc",
                table: "ApprovedInboxPollStates",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "ScopeFingerprint",
                table: "ApprovedInboxPollStates",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovedInboxPollStates",
                table: "ApprovedInboxPollStates",
                column: "ApprovedMailboxId");

            migrationBuilder.CreateTable(
                name: "ApprovedMailboxSubscriptions",
                columns: table => new
                {
                    ApprovedMailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LifecycleState = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LastMaintainedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastMaintenanceFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedMailboxSubscriptions", x => x.ApprovedMailboxId);
                    table.ForeignKey(
                        name: "FK_ApprovedMailboxSubscriptions_ApprovedMailboxes_ApprovedMailboxId",
                        column: x => x.ApprovedMailboxId,
                        principalTable: "ApprovedMailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "ApprovedMailboxes",
                keyColumn: "Id",
                keyValue: new Guid("49f47eb9-c5b0-464f-b8f0-8c90ba061728"),
                column: "ActivatedAtUtc",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPollStates_DueAtUtc_ApprovedMailboxId",
                table: "ApprovedInboxPollStates",
                columns: new[] { "DueAtUtc", "ApprovedMailboxId" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPoisonMessages_ApprovedMailboxId_OccurrenceKey",
                table: "ApprovedInboxPoisonMessages",
                columns: new[] { "ApprovedMailboxId", "OccurrenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxSubscriptions_ExpiresAtUtc",
                table: "ApprovedMailboxSubscriptions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxSubscriptions_SubscriptionId",
                table: "ApprovedMailboxSubscriptions",
                column: "SubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "CanonicalInternetMessageIdentity" },
                unique: true,
                filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "ImmutableMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "FolderScope", "ReceivedAtUtc", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.Sql(
                "IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL " +
                "GRANT SELECT ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_web_runtime_role];");
            migrationBuilder.Sql(
                "IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL " +
                "GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedMailboxSubscriptions] TO [pegasus_worker_runtime_role];");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_ApprovedMailboxId",
                table: "ApprovedInboxPoisonMessages",
                column: "ApprovedMailboxId",
                principalTable: "ApprovedInboxPollStates",
                principalColumn: "ApprovedMailboxId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovedInboxPollStates_ApprovedMailboxes_ApprovedMailboxId",
                table: "ApprovedInboxPollStates",
                column: "ApprovedMailboxId",
                principalTable: "ApprovedMailboxes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
                table: "RetainedMailboxMessages",
                column: "MailboxId",
                principalTable: "ApprovedInboxPollStates",
                principalColumn: "ApprovedMailboxId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_ApprovedMailboxId",
                table: "ApprovedInboxPoisonMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ApprovedInboxPollStates_ApprovedMailboxes_ApprovedMailboxId",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropForeignKey(
                name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropTable(
                name: "ApprovedMailboxSubscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ApprovedInboxPollStates",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropIndex(
                name: "IX_ApprovedInboxPollStates_DueAtUtc_ApprovedMailboxId",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropIndex(
                name: "IX_ApprovedInboxPoisonMessages_ApprovedMailboxId_OccurrenceKey",
                table: "ApprovedInboxPoisonMessages");

            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "ScopeFingerprint",
                table: "ApprovedInboxPollStates");

            migrationBuilder.RenameColumn(
                name: "ApprovedMailboxId",
                table: "ApprovedInboxPollStates",
                newName: "MailboxId");

            migrationBuilder.RenameColumn(
                name: "ApprovedMailboxId",
                table: "ApprovedInboxPoisonMessages",
                newName: "MailboxId");

            migrationBuilder.AlterColumn<string>(
                name: "MailboxId",
                table: "ApprovedInboxPollStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "MailboxId",
                table: "ApprovedInboxPoisonMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "MailboxId",
                table: "RetainedMailboxMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.Sql(
                "UPDATE state SET [MailboxId] = mailbox.[MailboxIdentity] " +
                "FROM [dbo].[ApprovedInboxPollStates] state JOIN [dbo].[ApprovedMailboxes] mailbox " +
                "ON mailbox.[Id] = TRY_CONVERT(uniqueidentifier, state.[MailboxId]);");
            migrationBuilder.Sql(
                "UPDATE poison SET [MailboxId] = mailbox.[MailboxIdentity] " +
                "FROM [dbo].[ApprovedInboxPoisonMessages] poison JOIN [dbo].[ApprovedMailboxes] mailbox " +
                "ON mailbox.[Id] = TRY_CONVERT(uniqueidentifier, poison.[MailboxId]);");
            migrationBuilder.Sql(
                "UPDATE retained SET [MailboxId] = mailbox.[MailboxIdentity] " +
                "FROM [dbo].[RetainedMailboxMessages] retained JOIN [dbo].[ApprovedMailboxes] mailbox " +
                "ON mailbox.[Id] = TRY_CONVERT(uniqueidentifier, retained.[MailboxId]);");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ApprovedInboxPollStates",
                table: "ApprovedInboxPollStates",
                column: "MailboxId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPollStates_DueAtUtc_MailboxId",
                table: "ApprovedInboxPollStates",
                columns: new[] { "DueAtUtc", "MailboxId" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedInboxPoisonMessages_MailboxId_OccurrenceKey",
                table: "ApprovedInboxPoisonMessages",
                columns: new[] { "MailboxId", "OccurrenceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "CanonicalInternetMessageIdentity" },
                unique: true,
                filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "ImmutableMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_FolderScope_ReceivedAtUtc_Id",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "FolderScope", "ReceivedAtUtc", "Id" },
                descending: new[] { false, false, true, false });

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_MailboxId",
                table: "ApprovedInboxPoisonMessages",
                column: "MailboxId",
                principalTable: "ApprovedInboxPollStates",
                principalColumn: "MailboxId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
                table: "RetainedMailboxMessages",
                column: "MailboxId",
                principalTable: "ApprovedInboxPollStates",
                principalColumn: "MailboxId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
