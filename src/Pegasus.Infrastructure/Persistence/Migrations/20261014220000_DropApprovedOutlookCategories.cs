using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Directly applying Outlook categories is out of scope (`MAIL-13` retired,
/// operator, 14 October 2026). Drops the `ApprovedOutlookCategories` allowlist
/// table; SQL Server drops its permission rows with it. Destructive and
/// forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261014220000_DropApprovedOutlookCategories")]
public partial class DropApprovedOutlookCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropTable(
            name: "ApprovedOutlookCategories");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "DropApprovedOutlookCategories is forward-only: it drops ApprovedOutlookCategories. Restore from an approved backup instead.");
}
