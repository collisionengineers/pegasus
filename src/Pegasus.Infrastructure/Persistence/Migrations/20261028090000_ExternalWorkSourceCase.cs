using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Triage Case's link to its instruction Case queues the copy of the
/// Triage Case's files onto that Case (FRD-03): the work's Case is the
/// instruction Case, and <c>SourceCaseId</c> names the Triage Case it copies
/// from. Additive: one nullable column, its index and its foreign key; no
/// data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261028090000_ExternalWorkSourceCase")]
public partial class ExternalWorkSourceCase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "SourceCaseId",
            table: "ExternalWorkItems",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExternalWorkItems_SourceCaseId",
            table: "ExternalWorkItems",
            column: "SourceCaseId");

        migrationBuilder.AddForeignKey(
            name: "FK_ExternalWorkItems_Cases_SourceCaseId",
            table: "ExternalWorkItems",
            column: "SourceCaseId",
            principalTable: "Cases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ExternalWorkItems_Cases_SourceCaseId",
            table: "ExternalWorkItems");

        migrationBuilder.DropIndex(
            name: "IX_ExternalWorkItems_SourceCaseId",
            table: "ExternalWorkItems");

        migrationBuilder.DropColumn(
            name: "SourceCaseId",
            table: "ExternalWorkItems");
    }
}
