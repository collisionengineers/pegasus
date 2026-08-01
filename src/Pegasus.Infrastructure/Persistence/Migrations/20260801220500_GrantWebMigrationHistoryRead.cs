using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260801220500_GrantWebMigrationHistoryRead")]
public sealed class GrantWebMigrationHistoryRead : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "GRANT SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] TO [pegasus_web_runtime_role];");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "REVOKE SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] FROM [pegasus_web_runtime_role];");
    }
}
