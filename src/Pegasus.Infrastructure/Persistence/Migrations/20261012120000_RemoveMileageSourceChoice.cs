using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Removes the staff pick of who advised a typed mileage (operator, 12 October
/// 2026): a figure staff typed is Custom, and the report's mileage source is
/// read off the mileage's own provenance alone. The path leaves the assessment
/// vocabulary, so its recorded values go with it. Data-only, destructive and
/// forward-only; the table's own constraint and grants are unchanged.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261012120000_RemoveMileageSourceChoice")]
public partial class RemoveMileageSourceChoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            DELETE FROM [CaseAssessmentFields]
            WHERE [FieldPath] = 'vehicle.mileage_source';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RemoveMileageSourceChoice is forward-only: it deletes the recorded values of a field that left the vocabulary. Restore from an approved backup instead.");
}
