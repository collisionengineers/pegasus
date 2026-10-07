using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Report date is one box (operator, 7 October 2026): a recorded date is
/// the date the report prints, and an empty one means the day it is
/// generated. The Override report date switch is gone from the assessment
/// vocabulary, so its recorded values go too.
/// An older release reads no row as the switch off, so it runs unchanged
/// beside this.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007160000_RemoveReportDateOverride")]
public partial class RemoveReportDateOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            DELETE FROM dbo.CaseAssessmentFields WHERE [FieldPath] = N'report.date_override';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RemoveReportDateOverride is forward-only: the override switch values are not restored.");
}
