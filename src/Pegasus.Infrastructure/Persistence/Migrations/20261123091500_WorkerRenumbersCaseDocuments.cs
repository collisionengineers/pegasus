using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Initial custody moves a document filed before it off the numbers it gave
/// the instruction's files (R719), which updates <c>CaseDocuments.Ordinal</c>.
/// The Worker's role held only SELECT and INSERT on that table, so production
/// refused the move on 23 November 2026 (R1227). Additive: no row changes and
/// the Web role is unchanged. Production already holds the grant; granting it
/// again changes nothing.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261123091500_WorkerRenumbersCaseDocuments")]
public partial class WorkerRenumbersCaseDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                "GRANT UPDATE ON OBJECT::[dbo].[CaseDocuments] TO [pegasus_worker_runtime_role];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                "REVOKE UPDATE ON OBJECT::[dbo].[CaseDocuments] FROM [pegasus_worker_runtime_role];");
        }
    }
}
