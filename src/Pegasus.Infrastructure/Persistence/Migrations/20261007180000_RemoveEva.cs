using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Removes EVA (operator, 7 October 2026; ADR-0064). The API submissions, the
/// automatic Review queue and the first-handoff proxy go, and so does the
/// Principal's report-generation route; the Case export is a plain download
/// and "Sent to Engineer" is read from the workflow events. SQL Server drops a
/// table's permission rows with the table, so it needs no REVOKE. Destructive
/// and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007180000_RemoveEva")]
public partial class RemoveEva : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropTable(
            name: "EvaSubmissions");
        migrationBuilder.DropTable(
            name: "AutomaticEvaReviewSubmissions");
        migrationBuilder.DropTable(
            name: "EvaFirstHandoffProxies");
        migrationBuilder.DropCheckConstraint(
            name: "CK_Principals_ReportGenerationPolicy",
            table: "Principals");
        migrationBuilder.DropColumn(
            name: "ReportGenerationPolicy",
            table: "Principals");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RemoveEva is forward-only: it drops the EVA tables and the Principal report-generation route. Restore from an approved backup instead.");
}
