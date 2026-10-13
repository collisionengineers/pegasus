using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Drops the Principal domain reference snapshot: three tables seeded once from
/// the July 2026 workbook that no runtime code has read since the catalog port
/// went (#1273). Recognition stays in code (operator, 13 October 2026): the
/// <c>Principals</c> row is where a Principal exists and
/// <c>PrincipalMailRoutePolicy</c> is the one route catalogue. SQL Server drops
/// a table's permission rows with the table, so the Worker's SELECT grants need
/// no REVOKE. Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261013100000_DropPrincipalDomainReference")]
public partial class DropPrincipalDomainReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropTable(
            name: "PrincipalDomainEvidence");

        migrationBuilder.DropTable(
            name: "PrincipalReferences");

        migrationBuilder.DropTable(
            name: "PrincipalDomainPackages");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "DropPrincipalDomainReference is forward-only: it drops PrincipalDomainEvidence, PrincipalReferences and PrincipalDomainPackages. Restore from an approved backup instead.");
}
