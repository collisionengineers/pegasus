using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class FilterActiveCaseReportGenerationSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CaseReportGenerations_CaseId_SnapshotHash",
            table: "CaseReportGenerations");

        migrationBuilder.CreateIndex(
            name: "IX_CaseReportGenerations_CaseId_SnapshotHash",
            table: "CaseReportGenerations",
            columns: new[] { "CaseId", "SnapshotHash" },
            unique: true,
            filter: "[State] <> N'Stale'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "This migration cannot be reverted after stale report generations may share a snapshot hash.");
    }
}
