using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// DOCS-014: a report preview or a confirmed-artifact download is Case
/// history, not a Case mutation — it records with an unchanged before/after
/// version, exactly as an operator note already does. The per-Case,
/// per-version uniqueness index must exempt the two new event types the same
/// way it already exempts <c>operator_note</c> and
/// <c>case_guidance_applied</c>, or the first write of either would collide
/// with whatever real mutation last touched the Case's current version.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260910104000_RecordCaseReportViewAndDownloadEvents")]
public partial class RecordCaseReportViewAndDownloadEvents : Migration
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
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded'");
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
            filter: "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied'");
    }
}
