using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Glass's session may rebuild a repair spec made outside Pegasus (operator,
/// 18 November 2026; R1171 to R1173): one nullable column holds the build, its
/// acts and what became of each, null meaning an ordinary launch, which is
/// what every session so far was. Additive: no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261118120000_GlassSessionBuild")]
public partial class GlassSessionBuild : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BuildJson",
            table: "GlassRepairEstimateSessions",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BuildJson", table: "GlassRepairEstimateSessions");
    }
}
