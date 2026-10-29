using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Case acceptance, which the Worker runs, reads the Administrator's automatic
/// Glass's valuation switch (R545, R546, 29 October 2026), a row of
/// <c>SendToAiControl</c>, so the Worker's role may read that table. Additive:
/// no row changes and the Web role is unchanged.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261029160000_WorkerReadsAutomaticValuationSwitch")]
public partial class WorkerReadsAutomaticValuationSwitch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                "GRANT SELECT ON OBJECT::[dbo].[SendToAiControl] TO [pegasus_worker_runtime_role];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                "REVOKE SELECT ON OBJECT::[dbo].[SendToAiControl] FROM [pegasus_worker_runtime_role];");
        }
    }
}
