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
            migrationBuilder.AddColumn<string>(
                name: "CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            // Historical retained rows predate the canonical key. Their RFC
            // identities are transport Message-IDs (ASCII in the accepted route),
            // so SQL trim + uppercase matches the Core canonical form for them.
            migrationBuilder.Sql(
                """
                UPDATE [RetainedMailboxMessages]
                SET [CanonicalInternetMessageIdentity] = UPPER(LTRIM(RTRIM([InternetMessageIdentity])))
                WHERE [InternetMessageIdentity] IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages",
                columns: new[] { "MailboxId", "CanonicalInternetMessageIdentity" },
                unique: true,
                filter: "[CanonicalInternetMessageIdentity] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RetainedMailboxMessages_MailboxId_CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages");

            migrationBuilder.DropColumn(
                name: "CanonicalInternetMessageIdentity",
                table: "RetainedMailboxMessages");
        }
    }
}
