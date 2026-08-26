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
            // Pre-release operational state is disposable. Start the stable mailbox identity
            // model cleanly instead of carrying Graph-keyed cursors into the target schema.
            migrationBuilder.Sql("DELETE FROM [dbo].[ApprovedInboxPoisonMessages];");
            migrationBuilder.Sql("DELETE FROM [dbo].[RetainedMailboxMessages];");
            migrationBuilder.Sql("DELETE FROM [dbo].[ApprovedInboxPollStates];");

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

            migrationBuilder.DropColumn(
                name: "MailboxId",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "MailboxId",
                table: "ApprovedInboxPoisonMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "MailboxId",
                table: "RetainedMailboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAtUtc",
                table: "ApprovedMailboxes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedMailboxId",
                table: "ApprovedInboxPollStates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedMailboxId",
                table: "ApprovedInboxPoisonMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "ApprovedMailboxId",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "ActivatedAtUtc",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "ScopeFingerprint",
                table: "ApprovedInboxPollStates");

            migrationBuilder.DropColumn(
                name: "ApprovedMailboxId",
                table: "ApprovedInboxPoisonMessages");

            migrationBuilder.AlterColumn<string>(
                name: "MailboxId",
                table: "RetainedMailboxMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "MailboxId",
                table: "ApprovedInboxPollStates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MailboxId",
                table: "ApprovedInboxPoisonMessages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

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
