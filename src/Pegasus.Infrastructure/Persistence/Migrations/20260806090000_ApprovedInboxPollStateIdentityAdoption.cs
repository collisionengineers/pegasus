using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Lets a mailbox's poll state be re-keyed to the identity an administrator
/// saved, carrying the rows that reference it with it.
/// </summary>
/// <remarks>
/// A mailbox polls under the deployment's configured fallback identity until
/// its real one is saved on the approved row, and one address has one poll
/// state. The claim therefore adopts the existing row rather than inserting a
/// second one for the same address, which is what preserves the delta cursor
/// across the change. Both references to that key were <c>NO ACTION</c> on
/// update, so the re-key was refused outright the moment a mailbox had retained
/// or quarantined anything. Cascading the update fixes that without widening
/// what the Worker may write: the engine performs the cascade, so no grant on
/// the referencing tables is needed, and deletes stay restricted.
/// </remarks>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260806090000_ApprovedInboxPollStateIdentityAdoption")]
public sealed class ApprovedInboxPollStateIdentityAdoption : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Recreate(migrationBuilder, onUpdate: "ON UPDATE CASCADE");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        Recreate(migrationBuilder, onUpdate: "ON UPDATE NO ACTION");
    }

    private static void Recreate(MigrationBuilder migrationBuilder, string onUpdate)
    {
        Recreate(
            migrationBuilder,
            "ApprovedInboxPoisonMessages",
            "FK_ApprovedInboxPoisonMessages_ApprovedInboxPollStates_MailboxId",
            onUpdate);
        Recreate(
            migrationBuilder,
            "RetainedMailboxMessages",
            "FK_RetainedMailboxMessages_ApprovedInboxPollStates_MailboxId",
            onUpdate);
    }

    private static void Recreate(
        MigrationBuilder migrationBuilder,
        string table,
        string constraint,
        string onUpdate)
    {
        migrationBuilder.Sql(
            $"ALTER TABLE [dbo].[{table}] DROP CONSTRAINT [{constraint}];");
        migrationBuilder.Sql(
            $"ALTER TABLE [dbo].[{table}] ADD CONSTRAINT [{constraint}] " +
            "FOREIGN KEY ([MailboxId]) " +
            "REFERENCES [dbo].[ApprovedInboxPollStates] ([MailboxId]) " +
            $"ON DELETE NO ACTION {onUpdate};");
    }
}
