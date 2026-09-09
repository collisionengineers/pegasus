using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909120000_ApprovedMailboxDefaultStaffSend")]
public partial class ApprovedMailboxDefaultStaffSend : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }

        migrationBuilder.AddColumn<bool>(
            name: "IsDefaultStaffSend",
            table: "ApprovedMailboxes",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddCheckConstraint(
            name: "CK_ApprovedMailboxes_DefaultStaffSendEligibility",
            table: "ApprovedMailboxes",
            sql: "[IsDefaultStaffSend] = 0 OR ([State] = 'Approved' AND [AllowStaffSend] = 1 AND [AllowSentEvidence] = 1 AND [ActivatedAtUtc] IS NOT NULL AND [MailboxIdentity] IS NOT NULL AND [SentFolderIdentity] IS NOT NULL AND [MailboxGeneration] > 0 AND [VerifiedEncodedMessageSizeLimit] IS NOT NULL AND [VerifiedEncodedMessageSizeLimit] > 0)");

        migrationBuilder.CreateIndex(
            name: "IX_ApprovedMailboxes_IsDefaultStaffSend",
            table: "ApprovedMailboxes",
            column: "IsDefaultStaffSend",
            unique: true,
            filter: "[IsDefaultStaffSend] = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }

        migrationBuilder.DropIndex(
            name: "IX_ApprovedMailboxes_IsDefaultStaffSend",
            table: "ApprovedMailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ApprovedMailboxes_DefaultStaffSendEligibility",
            table: "ApprovedMailboxes");

        migrationBuilder.DropColumn(
            name: "IsDefaultStaffSend",
            table: "ApprovedMailboxes");
    }
}
