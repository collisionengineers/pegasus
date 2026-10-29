using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A record edit scope keeps its token and the details of the claim that issued it, as the Case
/// lease does, so the holder's own claim resumes it with the same token (R518) and a replayed
/// claim returns it (R519, 29 October 2026). Additive: two nullable columns, no row changes; a
/// scope claimed before them is simply not resumable or replayable and lapses within five minutes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261029200000_EditScopeRetainsItsClaim")]
public partial class EditScopeRetainsItsClaim : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClaimRequestHash",
            table: "EditScopes",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Token",
            table: "EditScopes",
            type: "nchar(64)",
            fixedLength: true,
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClaimRequestHash",
            table: "EditScopes");

        migrationBuilder.DropColumn(
            name: "Token",
            table: "EditScopes");
    }
}
