using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Retires the report delivery preparation (operator, 6 October 2026): a
/// report is sent in one step, and the staff-send operation is the record of
/// what was sent. Nothing reads or writes <c>CaseReportDeliveryIntents</c>.
/// SQL Server drops a table's permission rows with the table, so it needs no
/// REVOKE. Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261006150000_DropCaseReportDeliveryIntents")]
public partial class DropCaseReportDeliveryIntents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropTable(
            name: "CaseReportDeliveryIntents");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "DropCaseReportDeliveryIntents is forward-only: it drops CaseReportDeliveryIntents. Restore from an approved backup instead.");
}
