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
        migrationBuilder.DropCheckConstraint(name: Constraint, table: Table);
        migrationBuilder.AddCheckConstraint(
            name: Constraint,
            table: Table,
            sql: "[Purpose] IN ('email', 'copy', 'handoff', 'session', 'text')");
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
