using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Worker's <c>-p</c> Box folder rename pass runs only once the
/// <c>CaseReferenceSuffix</c> migration is applied (#1895), which it reads
/// from <c>__EFMigrationsHistory</c>, so the Worker's role may read that
/// table as the Web's already does. Additive: no row changes and the Web role
/// is unchanged.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261108120000_WorkerReadsMigrationHistory")]
public partial class WorkerReadsMigrationHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                "GRANT SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] TO [pegasus_worker_runtime_role];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                "REVOKE SELECT ON OBJECT::[dbo].[__EFMigrationsHistory] FROM [pegasus_worker_runtime_role];");
        }
    }
}
