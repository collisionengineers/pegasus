using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Whether a kept e-mail's original has an HTML part, so the e-mail dialogue
/// knows which messages it can show as they were sent (R1277, R1279): one
/// nullable column, null on every row kept before it, which means not known.
/// Additive: no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261126120000_RetainedMailHasHtmlBody")]
public partial class RetainedMailHasHtmlBody : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "HasHtmlBody",
            table: "RetainedMailboxMessages",
            type: "bit",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HasHtmlBody", table: "RetainedMailboxMessages");
    }
}
