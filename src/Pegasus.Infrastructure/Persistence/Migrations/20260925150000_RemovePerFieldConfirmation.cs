using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// There is no per-field review (operator, 25 September 2026): Review is a
/// Case stage and Hand to Engineer is the review, so a recorded assessment
/// value is the Case's value whoever recorded it. The confirmation columns on
/// <c>CaseAssessmentFields</c> and <c>CaseEstimateLines</c> go, with the
/// check constraint that paired them, and so does <c>CaseFieldProposals</c>:
/// the Settlement Proposed column it fed is retired, and nothing could record
/// a proposal since automation stopped writing findings. An Automation value
/// on a professional-finding field was a proposal, never a finding, so those
/// rows are removed rather than promoted; the field history keeps them.
/// Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260925150000_RemovePerFieldConfirmation")]
public partial class RemovePerFieldConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        // The literals are the professional-finding paths (IsFinding) as of
        // this release, spelled out because a migration describes the rows a
        // past release wrote; Core now refuses an Automation write of any.
        migrationBuilder.Sql(
            """
            DELETE FROM dbo.CaseAssessmentFields
            WHERE [RecordedByKind] = N'Automation'
              AND [FieldPath] IN (
                  N'assessment.values.retail',
                  N'assessment.values.trade',
                  N'assessment.values.engineer',
                  N'assessment.outcome',
                  N'assessment.legal_status',
                  N'assessment.unroadworthy_reason',
                  N'assessment.category',
                  N'assessment.salvage_value');
            """);

        migrationBuilder.DropTable(name: "CaseFieldProposals");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseAssessmentFields_Confirmation",
            table: "CaseAssessmentFields");
        migrationBuilder.DropColumn(name: "ConfirmedAtUtc", table: "CaseAssessmentFields");
        migrationBuilder.DropColumn(name: "ConfirmedBy", table: "CaseAssessmentFields");
        migrationBuilder.DropColumn(name: "ConfirmedAtUtc", table: "CaseEstimateLines");
        migrationBuilder.DropColumn(name: "ConfirmedBy", table: "CaseEstimateLines");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RemovePerFieldConfirmation is forward-only: the confirmation columns and the proposals table are not restored.");
}
