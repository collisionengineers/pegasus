using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Work Centre's New cases section draws a "since you last looked" line
/// (Work Centre D8), so each account keeps when the person last opened the
/// Work Centre. A plain stamp on the account row: not a versioned edit, so it
/// never contends with an administrator's save of the same account.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913094324_WorkCentreLastSeen")]
public partial class WorkCentreLastSeen : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "WorkCentreLastSeenUtc",
            table: "AspNetUsers",
            type: "datetimeoffset",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "WorkCentreLastSeenUtc", table: "AspNetUsers");
    }
}
