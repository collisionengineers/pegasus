using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedMailboxGraphIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InboxFolderIdentity",
                table: "ApprovedMailboxes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailboxIdentity",
                table: "ApprovedMailboxes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SentFolderIdentity",
                table: "ApprovedMailboxes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ApprovedMailboxes",
                keyColumn: "Id",
                keyValue: new Guid("49f47eb9-c5b0-464f-b8f0-8c90ba061728"),
                columns: new[] { "InboxFolderIdentity", "MailboxIdentity", "SentFolderIdentity" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedMailboxes_MailboxIdentity",
                table: "ApprovedMailboxes",
                column: "MailboxIdentity",
                unique: true,
                filter: "[MailboxIdentity] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovedMailboxes_MailboxIdentity",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "InboxFolderIdentity",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "MailboxIdentity",
                table: "ApprovedMailboxes");

            migrationBuilder.DropColumn(
                name: "SentFolderIdentity",
                table: "ApprovedMailboxes");
        }
    }
}
