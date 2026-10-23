using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Case-filing replay reads active intake associations by kind and link
/// time, the pipeline's from the start of its two-hour window and a member of
/// staff's in full, and joins each to its receipt's work item, every minute.
/// The association index is filtered to active rows and keyed by kind, so the
/// pipeline's read is one range from the cutoff; the work-item index is a
/// seek per association (R367). Additive: two indexes, no data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261022210844_CaseFilingReplayIndexes")]
public partial class CaseFilingReplayIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_IntakeWorkItems_ProcessedReceiptId",
            table: "IntakeWorkItems",
            column: "ProcessedReceiptId")
            .Annotation("SqlServer:Include", new[] { "State", "StagedReceiptId" });

        migrationBuilder.CreateIndex(
            name: "IX_IntakeManualAssociations_ActorKind_LinkedAtUtc_IntakeReceiptId",
            table: "IntakeManualAssociations",
            columns: new[] { "ActorKind", "LinkedAtUtc", "IntakeReceiptId" },
            filter: "[IsActive] = 1")
            .Annotation("SqlServer:Include", new[] { "CaseId", "Version" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_IntakeManualAssociations_ActorKind_LinkedAtUtc_IntakeReceiptId",
            table: "IntakeManualAssociations");

        migrationBuilder.DropIndex(
            name: "IX_IntakeWorkItems_ProcessedReceiptId",
            table: "IntakeWorkItems");
    }
}
