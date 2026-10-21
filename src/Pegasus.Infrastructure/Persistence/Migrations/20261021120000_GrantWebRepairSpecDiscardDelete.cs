using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Discard deletes a non-Current repair spec with its lines and numbered versions
/// (FRD-25; operator, 11 October 2026). Replace the Web runtime role's DELETE denials
/// on <c>CaseRepairSpecifications</c> and <c>CaseRepairSpecificationSnapshots</c> with
/// grants; the Worker never discards and keeps its snapshot DELETE denial.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261021120000_GrantWebRepairSpecDiscardDelete")]
public partial class GrantWebRepairSpecDiscardDelete : Migration
{
    private static readonly string[] DeleteTables =
    [
        "CaseRepairSpecifications",
        "CaseRepairSpecificationSnapshots"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;
        foreach (var table in DeleteTables)
        {
            migrationBuilder.Sql($"REVOKE DELETE ON OBJECT::[dbo].[{table}] FROM [pegasus_web_runtime_role];");
            migrationBuilder.Sql($"GRANT DELETE ON OBJECT::[dbo].[{table}] TO [pegasus_web_runtime_role];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;
        foreach (var table in DeleteTables)
        {
            migrationBuilder.Sql($"REVOKE DELETE ON OBJECT::[dbo].[{table}] FROM [pegasus_web_runtime_role];");
            migrationBuilder.Sql($"DENY DELETE ON OBJECT::[dbo].[{table}] TO [pegasus_web_runtime_role];");
        }
    }
}
