using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Case's paged document list and paged history read newest first by
/// (CaseId, time, Id), the Work Centre reads every Open task by State, and the
/// pending-custody sweep reads the versions still Pending every minute. Each
/// read gets an index that answers it without a sort or a scan. Additive: four
/// indexes, no data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261018033213_KeysetPagingAndCustodySweepIndexes")]
public partial class KeysetPagingAndCustodySweepIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_DocumentVersions_CustodyStatus",
            table: "DocumentVersions",
            column: "CustodyStatus",
            filter: "[CustodyStatus] = N'Pending'")
            .Annotation("SqlServer:Include", new[] { "PendingContentStorageKey" });

        migrationBuilder.CreateIndex(
            name: "IX_DocumentOccurrences_CaseId_RecordedAtUtc_Id",
            table: "DocumentOccurrences",
            columns: new[] { "CaseId", "RecordedAtUtc", "Id" },
            descending: new[] { false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_CaseId_OccurredAtUtc_Id",
            table: "CaseWorkflowEvents",
            columns: new[] { "CaseId", "OccurredAtUtc", "Id" },
            descending: new[] { false, true, true });

        migrationBuilder.CreateIndex(
            name: "IX_CaseTasks_State",
            table: "CaseTasks",
            column: "State");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CaseTasks_State",
            table: "CaseTasks");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_CaseId_OccurredAtUtc_Id",
            table: "CaseWorkflowEvents");

        migrationBuilder.DropIndex(
            name: "IX_DocumentOccurrences_CaseId_RecordedAtUtc_Id",
            table: "DocumentOccurrences");

        migrationBuilder.DropIndex(
            name: "IX_DocumentVersions_CustodyStatus",
            table: "DocumentVersions");
    }
}
