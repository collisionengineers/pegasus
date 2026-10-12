using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// An approved mailbox is Incoming and/or Outgoing (operator, 12 October
/// 2026). Outgoing joins staff send and Sent-evidence reading, so a row is
/// Outgoing only where it held both. The default sender and the per-mailbox
/// verified size limit are removed: Case mail replies from the mailbox the
/// instruction arrived in, and every mailbox sends under one fixed limit.
/// Destructive: the dropped columns are not recoverable from this schema.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261012120000_MailboxIncomingOutgoing")]
public partial class MailboxIncomingOutgoing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropIndex(
            name: "IX_ApprovedMailboxes_IsDefaultStaffSend",
            table: "ApprovedMailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ApprovedMailboxes_DefaultStaffSendEligibility",
            table: "ApprovedMailboxes");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ApprovedMailboxes_SendLimit",
            table: "ApprovedMailboxes");

        migrationBuilder.Sql(
            """
            UPDATE [ApprovedMailboxes]
            SET [AllowStaffSend] = CASE WHEN [AllowStaffSend] = 1 AND [AllowSentEvidence] = 1 THEN 1 ELSE 0 END;
            """);

        migrationBuilder.RenameColumn(
            name: "AllowInboundIntake",
            table: "ApprovedMailboxes",
            newName: "AllowIncoming");

        migrationBuilder.RenameColumn(
            name: "AllowStaffSend",
            table: "ApprovedMailboxes",
            newName: "AllowOutgoing");

        migrationBuilder.DropColumn(
            name: "AllowSentEvidence",
            table: "ApprovedMailboxes");

        migrationBuilder.DropColumn(
            name: "IsDefaultStaffSend",
            table: "ApprovedMailboxes");

        migrationBuilder.DropColumn(
            name: "VerifiedEncodedMessageSizeLimit",
            table: "ApprovedMailboxes");

        migrationBuilder.DropColumn(
            name: "SendLimitVerifiedAtUtc",
            table: "ApprovedMailboxes");

        migrationBuilder.DropColumn(
            name: "SendLimitVerifiedBy",
            table: "ApprovedMailboxes");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "MailboxIncomingOutgoing is forward-only: restore from an approved backup instead.");
}
