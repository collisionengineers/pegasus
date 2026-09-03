using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendAssessmentVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields",
                sql: "[FieldPath] IN ('assessment.category', 'assessment.impact_location', 'assessment.impact_severity', 'assessment.legal_status', 'assessment.outcome', 'assessment.salvage_value', 'assessment.unroadworthy_reason', 'assessment.values.engineer', 'assessment.values.retail', 'assessment.values.trade', 'costs.recovery_charge', 'costs.repairer_vat_registered', 'costs.storage_charge', 'damage.impacts', 'damage.material_transfer', 'damage.tyres.centre_belt', 'damage.tyres.left_front.belt', 'damage.tyres.left_front.tyre', 'damage.tyres.left_rear.belt', 'damage.tyres.left_rear.tyre', 'damage.tyres.right_front.belt', 'damage.tyres.right_front.tyre', 'damage.tyres.right_rear.belt', 'damage.tyres.right_rear.tyre', 'damage.tyres.spare', 'damage.unrelated', 'damage.unrelated_deduction', 'engineer.name', 'engineer.qualifications', 'engineer.signature', 'fee.agreed_fee', 'fee.description_lines', 'incident.assessed', 'narrative.engineers_comments', 'narrative.history_check', 'narrative.nature_of_incident', 'rates.card', 'rates.class', 'rates.manufacturer_approved', 'rates.regional_uplift', 'settlement.betterment', 'settlement.claimant_vat_registered', 'settlement.diminution', 'settlement.excess', 'settlement.hire_daily_cost', 'settlement.hire_start', 'settlement.repair_delays', 'settlement.report_delay', 'settlement.reserve', 'settlement.salvage.agent', 'settlement.salvage.agent_reference', 'settlement.salvage.at', 'settlement.salvage.moved', 'settlement.salvage.owner_retains', 'settlement.salvage.settled', 'settlement.salvage.value_agreed', 'settlement.storage_per_day', 'statement_of_truth', 'vehicle.airbags_deployed', 'vehicle.body', 'vehicle.colour', 'vehicle.condition', 'vehicle.engine_cc', 'vehicle.fault_codes', 'vehicle.fuel', 'vehicle.mileage_source', 'vehicle.mot_expiry', 'vehicle.tax_expiry', 'vehicle.temporary_repair_cost', 'vehicle.temporary_repair_method', 'vehicle.temporary_repairs_possible', 'vehicle.transmission', 'vehicle.vehicle_type', 'vehicle.vin', 'vehicle.vin_checked', 'vehicle.year')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields",
                sql: "[FieldPath] IN ('assessment.category', 'assessment.impact_location', 'assessment.impact_severity', 'assessment.legal_status', 'assessment.outcome', 'assessment.salvage_value', 'assessment.unroadworthy_reason', 'assessment.values.engineer', 'assessment.values.retail', 'assessment.values.trade', 'costs.recovery_charge', 'costs.repairer_vat_registered', 'costs.storage_charge', 'engineer.name', 'engineer.qualifications', 'engineer.signature', 'fee.agreed_fee', 'fee.description_lines', 'incident.assessed', 'narrative.engineers_comments', 'narrative.history_check', 'narrative.nature_of_incident', 'rates.card', 'rates.class', 'rates.manufacturer_approved', 'rates.regional_uplift', 'statement_of_truth', 'vehicle.condition', 'vehicle.engine_cc', 'vehicle.fuel', 'vehicle.mileage_source', 'vehicle.vehicle_type', 'vehicle.vin', 'vehicle.year')");
        }
    }
}
