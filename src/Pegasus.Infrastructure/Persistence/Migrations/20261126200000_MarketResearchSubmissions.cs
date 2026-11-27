using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Market research submitted from Pegasus Desktop (issue 2166; R1238, R1309
/// to R1315): a table of submissions and one of their files, the Case's
/// market-research subfolder, and the subfolder a document is kept in. The
/// folder check on CaseDocuments is left as it is; the subfolder has a check
/// of its own. A submission's Case history event changes nothing on the Case
/// and carries the Case's version as it stands, so it joins the event types
/// the unique version index leaves out. Additive: two new tables, two
/// nullable columns and that index swapped for the same index with a narrower
/// filter; no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261126200000_MarketResearchSubmissions")]
public partial class MarketResearchSubmissions : Migration
{
    private const string EventsTable = "CaseWorkflowEvents";
    private const string VersionIndex = "IX_CaseWorkflowEvents_CaseId_AfterVersion";
    private const string PreviousVersionFilter =
        "[EventType] <> 'operator_note' AND [EventType] <> 'case_guidance_applied' "
        + "AND [EventType] <> 'case_report_draft_previewed' AND [EventType] <> 'case_report_artifact_downloaded' "
        + "AND [EventType] <> 'case_estimate_document_previewed' AND [EventType] <> 'edit_lease_taken_over' AND [EventType] <> 'market_research_attached' "
        + "AND [EventType] <> 'correspondence_sent' "
        + "AND [EventType] <> 'instruction_moved_to_deleted_items' AND [EventType] <> 'instruction_move_failed' "
        + "AND [EventType] <> 'correspondence_moved_to_deleted_items' AND [EventType] <> 'correspondence_move_failed'";
    private const string VersionFilter = PreviousVersionFilter + " AND [EventType] <> 'market_research_submitted'";
    private static readonly string[] VersionColumns = ["CaseId", "AfterVersion"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: VersionIndex, table: EventsTable);
        migrationBuilder.CreateIndex(
            name: VersionIndex,
            table: EventsTable,
            columns: VersionColumns,
            unique: true,
            filter: VersionFilter);

        migrationBuilder.AddColumn<string>(
            name: "MarketResearchCustodyRemoteId",
            table: "Cases",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustodySubfolder",
            table: "CaseDocuments",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseDocuments_CustodySubfolder",
            table: "CaseDocuments",
            sql: "[CustodySubfolder] IS NULL OR ([CustodySubfolder] = N'market-research' AND [CustodyFolder] = N'case')");

        migrationBuilder.CreateTable(
            name: "MarketResearchSubmissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                InputHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                SubmittedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Review = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                GuideMonth = table.Column<DateOnly>(type: "date", nullable: false),
                ProposedValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                RecordJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ClosedAiJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MarketResearchSubmissions", x => x.Id);
                table.CheckConstraint("CK_MarketResearchSubmissions_ProposedValue", "[ProposedValue] > 0");
                table.CheckConstraint("CK_MarketResearchSubmissions_Review", "[Review] IN ('Accepted', 'NotReviewed')");
                table.ForeignKey(
                    name: "FK_MarketResearchSubmissions_Cases_CaseId",
                    column: x => x.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MarketResearchSubmissionFiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Position = table.Column<int>(type: "int", nullable: false),
                Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                AdvertNumber = table.Column<int>(type: "int", nullable: true),
                OccurrenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MarketResearchSubmissionFiles", x => x.Id);
                table.CheckConstraint("CK_MarketResearchSubmissionFiles_Kind", "[Kind] IN ('Report', 'EvidencePack', 'Screenshot')");
                table.ForeignKey(
                    name: "FK_MarketResearchSubmissionFiles_DocumentOccurrences_OccurrenceId",
                    column: x => x.OccurrenceId,
                    principalTable: "DocumentOccurrences",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MarketResearchSubmissionFiles_DocumentVersions_VersionId",
                    column: x => x.VersionId,
                    principalTable: "DocumentVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_MarketResearchSubmissionFiles_MarketResearchSubmissions_SubmissionId",
                    column: x => x.SubmissionId,
                    principalTable: "MarketResearchSubmissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MarketResearchSubmissions_CaseId_OperationKey",
            table: "MarketResearchSubmissions",
            columns: new[] { "CaseId", "OperationKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MarketResearchSubmissionFiles_OccurrenceId",
            table: "MarketResearchSubmissionFiles",
            column: "OccurrenceId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MarketResearchSubmissionFiles_SubmissionId_Position",
            table: "MarketResearchSubmissionFiles",
            columns: new[] { "SubmissionId", "Position" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MarketResearchSubmissionFiles_VersionId",
            table: "MarketResearchSubmissionFiles",
            column: "VersionId");

        // The runtime roles' grants on the new tables (RuntimePermissions.csv):
        // the Web files a submission and reads it back; a row is written once,
        // so nothing changes or deletes one, and the Worker never touches one.
        migrationBuilder.Sql("""
            GRANT SELECT, INSERT ON OBJECT::[dbo].[MarketResearchSubmissions] TO [pegasus_web_runtime_role];
            DENY UPDATE, DELETE ON OBJECT::[dbo].[MarketResearchSubmissions] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[MarketResearchSubmissions] TO [pegasus_worker_runtime_role];
            GRANT SELECT, INSERT ON OBJECT::[dbo].[MarketResearchSubmissionFiles] TO [pegasus_web_runtime_role];
            DENY UPDATE, DELETE ON OBJECT::[dbo].[MarketResearchSubmissionFiles] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[MarketResearchSubmissionFiles] TO [pegasus_worker_runtime_role];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: VersionIndex, table: EventsTable);
        migrationBuilder.CreateIndex(
            name: VersionIndex,
            table: EventsTable,
            columns: VersionColumns,
            unique: true,
            filter: PreviousVersionFilter);
        migrationBuilder.DropTable(name: "MarketResearchSubmissionFiles");
        migrationBuilder.DropTable(name: "MarketResearchSubmissions");
        migrationBuilder.DropCheckConstraint(name: "CK_CaseDocuments_CustodySubfolder", table: "CaseDocuments");
        migrationBuilder.DropColumn(name: "CustodySubfolder", table: "CaseDocuments");
        migrationBuilder.DropColumn(name: "MarketResearchCustodyRemoteId", table: "Cases");
    }
}
