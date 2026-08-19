using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainedMailboxInternetMessageIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_InternetMessageIdentity",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "InternetMessageIdentity" },
                unique: true,
                filter: "[InternetMessageIdentity] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_InternetMessageIdentity",
                table: "RetainedMailboxMessages");
        }
    }
}
