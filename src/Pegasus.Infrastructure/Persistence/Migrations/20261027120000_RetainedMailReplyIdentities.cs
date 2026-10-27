using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The exact Message-IDs a retained message answers (its In-Reply-To and
/// References headers), so automatic mail-to-Case association can link a reply
/// to the Case of the message it answers, across any approved mailbox (D13,
/// item T; operator, 27 October 2026). Additive and nullable: rows retained
/// before this migration have no reply identities and link only by the signals
/// they already carried.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261027120000_RetainedMailReplyIdentities")]
public partial class RetainedMailReplyIdentities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "InReplyToIdentitiesJson",
            table: "RetainedMailboxMessages",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "InReplyToIdentitiesJson",
            table: "RetainedMailboxMessages");
    }
}
