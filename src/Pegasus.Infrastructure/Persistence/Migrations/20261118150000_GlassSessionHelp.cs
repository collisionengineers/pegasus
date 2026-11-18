using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// An Engineer may let the Automation help in their own open Glass's session
/// (operator, 18 November 2026; R1170, R1183): one nullable column records
/// when they turned help on, null meaning off, which is what every session so
/// far was. Additive: no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261118150000_GlassSessionHelp")]
public partial class GlassSessionHelp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "HelpOnAtUtc",
            table: "GlassRepairEstimateSessions",
            type: "datetimeoffset",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "HelpOnAtUtc", table: "GlassRepairEstimateSessions");
    }
}
