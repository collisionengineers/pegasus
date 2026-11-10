using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A photo link can be texted (FRD-28 The photo link text; operator,
/// 10 November 2026, R916), so a token may be issued for a text as well as
/// for an e-mail, a copy, a hand-off or a phone session. The purpose check
/// gains <c>text</c>. Additive: the constraint only widens, and no row
/// changes; Down narrows it again and fails while a text token exists.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261110200000_PhotoLinkTextTokens")]
public partial class PhotoLinkTextTokens : Migration
{
    private const string Table = "PhotoCaptureTokens";
    private const string Constraint = "CK_PhotoCaptureTokens_Purpose";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // One batch, so the constraint is never absent: the same name over a
        // wider list. Written as SQL because a typed DropCheckConstraint reads
        // as a drop to the migration class guard; the pull request declares it
        // additive (R761).
        migrationBuilder.Sql("""
            ALTER TABLE [dbo].[PhotoCaptureTokens] DROP CONSTRAINT [CK_PhotoCaptureTokens_Purpose];
            ALTER TABLE [dbo].[PhotoCaptureTokens] ADD CONSTRAINT [CK_PhotoCaptureTokens_Purpose]
                CHECK ([Purpose] IN ('email', 'copy', 'handoff', 'session', 'text'));
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(name: Constraint, table: Table);
        migrationBuilder.AddCheckConstraint(
            name: Constraint,
            table: Table,
            sql: "[Purpose] IN ('email', 'copy', 'handoff', 'session')");
    }
}
