using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A colleague taking over a Case edit lease is Case history rather than a
/// Case mutation: it records the takeover at the Case's current version. Exempt
/// it from the per-Case, per-version uniqueness index, which otherwise refused
/// every takeover on a Case whose current version already had its own event.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260925190000_EditLeaseTakeoverHistoryEvents")]
public partial class EditLeaseTakeoverHistoryEvents : Migration
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
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over'");
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
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' AND [EventType] <> 'case_estimate_document_previewed'");
    }
}
