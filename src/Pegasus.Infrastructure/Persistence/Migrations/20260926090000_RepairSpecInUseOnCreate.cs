using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A repair spec a staff member creates is in use at once and stays editable
/// (operator, 25 September 2026), so there is no acceptance: a specification
/// is live (Draft) or Discarded, and Current is its own flag. Accepted and
/// Superseded rows become Draft; the unused LegacyUnresolved and
/// ApprovedAiProposal routes become Manual. The acceptance, supersession and
/// frozen-calculation columns go with their check constraint, as do the
/// estimate line's status and its write-only off-pattern copy. Destructive and
/// forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260926090000_RepairSpecInUseOnCreate")]
public partial class RepairSpecInUseOnCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseRepairSpecifications_Acceptance",
            table: "CaseRepairSpecifications");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseRepairSpecifications_Current",
            table: "CaseRepairSpecifications");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseRepairSpecifications_SourceRoute",
            table: "CaseRepairSpecifications");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseRepairSpecifications_State",
            table: "CaseRepairSpecifications");
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseEstimateLines_Status",
            table: "CaseEstimateLines");

        migrationBuilder.Sql(
            """
            UPDATE dbo.CaseRepairSpecifications
            SET [State] = N'Draft'
            WHERE [State] IN (N'Accepted', N'Superseded');

            UPDATE dbo.CaseRepairSpecifications
            SET [SourceRoute] = N'Manual'
            WHERE [SourceRoute] IN (N'LegacyUnresolved', N'ApprovedAiProposal');
            """);

        foreach (var column in new[]
        {
            "AcceptedAtUtc",
            "AcceptedBy",
            "CalculationBreakdownJson",
            "CalculationLabour",
            "CalculationPaintMaterials",
            "CalculationParts",
            "CalculationPolicyVersion",
            "CalculationSpecialistOther",
            "CalculationTotal",
            "CalculationVat",
            "RepairerVatRegistered",
            "SupersedesSpecificationId",
            "SupersessionReason",
            "VatOverrideReason",
        })
        {
            migrationBuilder.DropColumn(name: column, table: "CaseRepairSpecifications");
        }
        migrationBuilder.DropColumn(name: "CurrentValuesJson", table: "CaseEstimateLines");
        migrationBuilder.DropColumn(name: "Status", table: "CaseEstimateLines");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseRepairSpecifications_Current",
            table: "CaseRepairSpecifications",
            sql: "[IsCurrent] = 0 OR [State] = 'Draft'");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseRepairSpecifications_Discard",
            table: "CaseRepairSpecifications",
            sql: "([State] = 'Draft' AND [DiscardedBy] IS NULL AND [DiscardedAtUtc] IS NULL AND [DiscardReason] IS NULL) OR ([State] = 'Discarded' AND [DiscardedBy] IS NOT NULL AND [DiscardedAtUtc] IS NOT NULL AND [DiscardReason] IS NOT NULL)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseRepairSpecifications_SourceRoute",
            table: "CaseRepairSpecifications",
            sql: "[SourceRoute] IN ('Manual', 'Glasses', 'AudatexPdf', 'Json', 'AiDraft')");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseRepairSpecifications_State",
            table: "CaseRepairSpecifications",
            sql: "[State] IN ('Draft', 'Discarded')");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RepairSpecInUseOnCreate is forward-only: the acceptance and calculation columns are not restored.");
}
