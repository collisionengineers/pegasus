using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Create audit (decided 13 September): an Inspection + Audit Case gets a
/// separate linked Audit Case, a Case in its own right whose reference is the
/// <c>a.</c>/<c>ap.</c> value and which shares the original's principal, year
/// and sequence. <c>AuditOfCaseId</c> links it back, one Audit Case per
/// original. Sequence uniqueness therefore ignores linked Audit Cases — the
/// filtered index — while the reference stays unique across every Case. No
/// sequence is consumed and <c>CaseIdentityAllocator</c> is not involved. No
/// grant changes: the runtime roles already write Cases.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913101413_LinkedAuditCase")]
public partial class LinkedAuditCase : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Cases_SequenceLineageId_Year_Sequence",
            table: "Cases");

        migrationBuilder.AddColumn<Guid>(
            name: "AuditOfCaseId",
            table: "Cases",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Cases_AuditOfCaseId",
            table: "Cases",
            column: "AuditOfCaseId",
            unique: true,
            filter: "[AuditOfCaseId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Cases_SequenceLineageId_Year_Sequence",
            table: "Cases",
            columns: new[] { "SequenceLineageId", "Year", "Sequence" },
            unique: true,
            filter: "[AuditOfCaseId] IS NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_Cases_Cases_AuditOfCaseId",
            table: "Cases",
            column: "AuditOfCaseId",
            principalTable: "Cases",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Cases_Cases_AuditOfCaseId", table: "Cases");
        migrationBuilder.DropIndex(name: "IX_Cases_AuditOfCaseId", table: "Cases");
        migrationBuilder.DropIndex(name: "IX_Cases_SequenceLineageId_Year_Sequence", table: "Cases");
        migrationBuilder.DropColumn(name: "AuditOfCaseId", table: "Cases");
        migrationBuilder.CreateIndex(
            name: "IX_Cases_SequenceLineageId_Year_Sequence",
            table: "Cases",
            columns: new[] { "SequenceLineageId", "Year", "Sequence" },
            unique: true);
    }
}
