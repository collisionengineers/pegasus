using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Data repair for <c>20260826151807_ApprovedMailboxStableIdentityAndSubscriptions</c>,
    /// whose seed diff set <c>ActivatedAtUtc</c> to <c>NULL</c> on an approved mailbox that
    /// already had its Graph identities bound. Every intake consumer requires an activation
    /// time, so that mailbox was neither polled nor subscribed. Activation is restored at the
    /// moment this migration runs; mail received earlier is skipped by design.
    /// </summary>
    public partial class ReactivateBoundApprovedMailboxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [dbo].[ApprovedMailboxes] SET [ActivatedAtUtc] = SYSDATETIMEOFFSET() " +
                "WHERE [State] = N'Approved' AND [ActivatedAtUtc] IS NULL " +
                "AND [MailboxIdentity] IS NOT NULL AND [InboxFolderIdentity] IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
