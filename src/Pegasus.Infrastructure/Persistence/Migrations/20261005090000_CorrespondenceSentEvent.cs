using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A general correspondence send leaves a Notes line on its Case once the
/// Sent-evidence poll observes the Sent item (FRD-16 Notes, FRD-21). It
/// records itself in the Case history at the Case's current version, as an
/// operator note does, so it is exempt from the per-Case, per-version
/// uniqueness index.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261005090000_CorrespondenceSentEvent")]
public partial class CorrespondenceSentEvent : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents",
            columns: new[] { "CaseId", "AfterVersion" },
            unique: true,
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached' AND [EventType] <> 'correspondence_sent'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_CaseId_AfterVersion",
            table: "CaseWorkflowEvents",
            columns: new[] { "CaseId", "AfterVersion" },
            unique: true,
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached'");
    }
}
