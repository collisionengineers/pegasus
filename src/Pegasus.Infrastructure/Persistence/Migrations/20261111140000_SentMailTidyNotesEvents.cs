using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// What the two sent-mail tidies moved to Deleted Items, or could not, is
/// shown on the Case's Notes tab (operator, 11 November 2026; R959), so it is
/// written to <c>CaseWorkflowEvents</c>. It changes nothing on the Case and
/// carries the Case's version as it stands, so the four event types join the
/// ones the unique version index leaves out. Additive: the index is swapped
/// for the same index with a narrower filter, and no row changes.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261111140000_SentMailTidyNotesEvents")]
public partial class SentMailTidyNotesEvents : Migration
{
    private const string Table = "CaseWorkflowEvents";
    private const string Index = "IX_CaseWorkflowEvents_CaseId_AfterVersion";
    private const string PreviousFilter =
        "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' "
        + "AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' "
        + "AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached' "
        + "AND [EventType] <> 'correspondence_sent'";
    private const string Filter = PreviousFilter
        + " AND [EventType] <> 'instruction_moved_to_deleted_items' AND [EventType] <> 'instruction_move_failed'"
        + " AND [EventType] <> 'correspondence_moved_to_deleted_items' AND [EventType] <> 'correspondence_move_failed'";
    private static readonly string[] Columns = ["CaseId", "AfterVersion"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: Index, table: Table);
        migrationBuilder.CreateIndex(
            name: Index,
            table: Table,
            columns: Columns,
            unique: true,
            filter: Filter);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: Index, table: Table);
        migrationBuilder.CreateIndex(
            name: Index,
            table: Table,
            columns: Columns,
            unique: true,
            filter: PreviousFilter);
    }
}
