using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Removes the Case page facts no generated document prints (operator, 9
/// October 2026): the salvage logistics, excess, betterment, reserve,
/// diminution, hire, delays, the lump storage charge, temporary repairs,
/// tyres and seat belts, airbags, material transfer and the unrelated-damage
/// deduction. The paths leave the assessment vocabulary, so their recorded
/// values go with them. Data-only, destructive and forward-only; the table's
/// own constraint and grants are unchanged.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261009120000_RemoveUnprintedAssessmentFields")]
public partial class RemoveUnprintedAssessmentFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            DELETE FROM [CaseAssessmentFields]
            WHERE [FieldPath] IN (
                'settlement.salvage.at', 'settlement.salvage.agent', 'settlement.salvage.agent_reference',
                'settlement.salvage.moved', 'settlement.salvage.owner_retains', 'settlement.salvage.value_agreed',
                'settlement.salvage.settled',
                'settlement.excess', 'settlement.betterment', 'settlement.reserve', 'settlement.diminution',
                'settlement.hire_start', 'settlement.hire_daily_cost', 'settlement.repair_delays',
                'settlement.report_delay', 'costs.storage_charge',
                'vehicle.temporary_repairs_possible', 'vehicle.temporary_repair_method', 'vehicle.temporary_repair_cost',
                'vehicle.airbags_deployed', 'damage.material_transfer', 'damage.unrelated_deduction',
                'damage.tyres.right_front.tyre', 'damage.tyres.left_front.tyre',
                'damage.tyres.right_rear.tyre', 'damage.tyres.left_rear.tyre',
                'damage.tyres.right_front.belt', 'damage.tyres.left_front.belt',
                'damage.tyres.right_rear.belt', 'damage.tyres.left_rear.belt',
                'damage.tyres.spare', 'damage.tyres.centre_belt');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RemoveUnprintedAssessmentFields is forward-only: it deletes the recorded values of fields that left the vocabulary. Restore from an approved backup instead.");
}
