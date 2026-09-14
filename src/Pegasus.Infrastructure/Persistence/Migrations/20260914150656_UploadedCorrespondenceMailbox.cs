using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UploadedCorrespondenceMailbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "MailboxId",
                table: "RetainedMailboxMessages",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "ImmutableMessageId" },
                unique: true,
                filter: "[MailboxId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "MailboxId",
                table: "RetainedMailboxMessages",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_ImmutableMessageId",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "ImmutableMessageId" },
                unique: true);
        }
    }
}
