using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The rest of the Unknown repairer VAT status goes (R386, 22 October 2026):
/// frozen repair spec versions that say Unknown are rewritten to Registered,
/// as <c>EstimateVatPolicy.Standing()</c> records a restored one. Hand-picked
/// categories stay; otherwise the version takes Registered's own categories.
/// A live repair spec still stored as Unknown becomes Registered with its
/// category flags kept, as 20261019120000 did. The retired
/// <c>TreatmentPending</c> member leaves each rewritten version. A version's
/// content hash is only compared with a fresh freeze, so it is left as it
/// was. Forward-only: a Registered version that used to be Unknown cannot be
/// told apart from one frozen as Registered.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261023150000_RepairerVatStatusUnknownRemoved")]
public class RepairerVatStatusUnknownRemoved : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql("""
            UPDATE [dbo].[CaseRepairSpecifications]
            SET [RepairerVatStatus] = N'Registered'
            WHERE [RepairerVatStatus] = N'Unknown';
            """);

        // EstimateDetails serialises its recorded Vat and the VatPolicy it
        // stands on; each path is rewritten on its own (15 is every category).
        foreach (var path in new[] { "$.Vat", "$.VatPolicy" })
        {
            migrationBuilder.Sql($"""
                UPDATE frozen
                SET [DetailsJson] = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(frozen.[DetailsJson],
                        N'{path}.RepairerStatus', 1),
                        N'{path}.Categories', standing.Categories),
                        N'{path}.CategoriesOverridden', CONVERT(bit, CASE WHEN standing.Categories <> 15 THEN 1 ELSE 0 END)),
                        N'{path}.TreatmentPending', NULL)
                FROM [dbo].[CaseRepairSpecificationSnapshots] AS frozen
                CROSS APPLY (
                    SELECT CASE
                        WHEN JSON_VALUE(frozen.[DetailsJson], N'{path}.CategoriesOverridden') = N'true'
                            THEN CONVERT(int, JSON_VALUE(frozen.[DetailsJson], N'{path}.Categories'))
                        ELSE 15
                    END AS Categories) AS standing
                WHERE JSON_VALUE(frozen.[DetailsJson], N'{path}.RepairerStatus') = N'0';
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RepairerVatStatusUnknownRemoved is forward-only: repair specs and frozen versions stored as Unknown were rewritten to Registered. Restore from an approved backup instead.");
}
