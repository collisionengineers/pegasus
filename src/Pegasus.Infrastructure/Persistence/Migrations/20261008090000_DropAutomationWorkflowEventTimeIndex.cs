using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Work Centre's New cases feed lists new Cases only (operator, 8 October
/// 2026), so nothing reads the Automation actor's workflow events by time and
/// the index 20260928100000_WorkCentreQueryIndexes made for it goes.
/// Additive: an index only, no data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261008090000_DropAutomationWorkflowEventTimeIndex")]
public partial class DropAutomationWorkflowEventTimeIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_ActorKind_OccurredAtUtc",
            table: "CaseWorkflowEvents");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_ActorKind_OccurredAtUtc",
            table: "CaseWorkflowEvents",
            columns: new[] { "ActorKind", "OccurredAtUtc" })
            .Annotation("SqlServer:Include", new[] { "CaseId", "EventType", "BeforeVersion", "AfterVersion" });
    }
}
