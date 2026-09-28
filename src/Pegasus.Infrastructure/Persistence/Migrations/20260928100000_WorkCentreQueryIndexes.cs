using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Work Centre reads every queue by Case workflow state, and its New
/// cases feed reads Cases by creation time and the Automation actor's
/// workflow events by time. Each read gets an index that answers it without
/// scanning the table. Additive: three indexes, no data change.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260928100000_WorkCentreQueryIndexes")]
public partial class WorkCentreQueryIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflows_State",
            table: "CaseWorkflows",
            column: "State");

        migrationBuilder.CreateIndex(
            name: "IX_CaseWorkflowEvents_ActorKind_OccurredAtUtc",
            table: "CaseWorkflowEvents",
            columns: new[] { "ActorKind", "OccurredAtUtc" })
            .Annotation("SqlServer:Include", new[] { "CaseId", "EventType", "BeforeVersion", "AfterVersion" });

        migrationBuilder.CreateIndex(
            name: "IX_Cases_CreatedAtUtc",
            table: "Cases",
            column: "CreatedAtUtc")
            .Annotation("SqlServer:Include", new[] { "Type" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Cases_CreatedAtUtc",
            table: "Cases");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflowEvents_ActorKind_OccurredAtUtc",
            table: "CaseWorkflowEvents");

        migrationBuilder.DropIndex(
            name: "IX_CaseWorkflows_State",
            table: "CaseWorkflows");
    }
}
