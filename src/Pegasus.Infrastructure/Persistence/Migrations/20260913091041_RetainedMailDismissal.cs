using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Dismiss on the Inbox (planning, 13 September): a message row or record can be
/// dismissed, which takes it out of the incoming scopes without classifying or
/// linking it, and restored from the Dismissed scope. The retained message keeps
/// when and by whom; the act itself is an action-history entry. Nothing is
/// deleted, and no receipt or classification changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913091041_RetainedMailDismissal")]
public partial class RetainedMailDismissal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DismissedAtUtc",
            table: "RetainedMailboxMessages",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DismissedBySubjectId",
            table: "RetainedMailboxMessages",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DismissedAtUtc", table: "RetainedMailboxMessages");
        migrationBuilder.DropColumn(name: "DismissedBySubjectId", table: "RetainedMailboxMessages");
    }
}
