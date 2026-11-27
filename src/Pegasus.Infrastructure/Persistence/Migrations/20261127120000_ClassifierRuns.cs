using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The mail classifier records its own runs (FRD-08; operator, 27 November
/// 2026; R1318): a table of runs and one of the messages each run dealt with.
/// Additive: two new tables; no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261127120000_ClassifierRuns")]
public partial class ClassifierRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClassifierRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                FinishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                DryRun = table.Column<bool>(type: "bit", nullable: false),
                Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Batch = table.Column<int>(type: "int", nullable: false),
                ScanPages = table.Column<int>(type: "int", nullable: false),
                MaxBudgetUsd = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                AgentVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Outcome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                OutcomeKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                Eligible = table.Column<int>(type: "int", nullable: true),
                AwaitingDecision = table.Column<int>(type: "int", nullable: true),
                CandidatesHidden = table.Column<bool>(type: "bit", nullable: false),
                HiddenRuns = table.Column<int>(type: "int", nullable: false),
                Verdicts = table.Column<int>(type: "int", nullable: true),
                Failed = table.Column<int>(type: "int", nullable: true),
                Deferred = table.Column<int>(type: "int", nullable: true),
                Acted = table.Column<int>(type: "int", nullable: true),
                Turns = table.Column<int>(type: "int", nullable: true),
                CostUsd = table.Column<decimal>(type: "decimal(12,6)", precision: 12, scale: 6, nullable: true),
                DurationMs = table.Column<int>(type: "int", nullable: true),
                Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClassifierRuns", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ClassifierRunItems",
            columns: table => new
            {
                RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Ordinal = table.Column<int>(type: "int", nullable: false),
                MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Refusal = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClassifierRunItems", x => new { x.RunId, x.Ordinal });
                table.ForeignKey(
                    name: "FK_ClassifierRunItems_ClassifierRuns_RunId",
                    column: x => x.RunId,
                    principalTable: "ClassifierRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClassifierRuns_StartedAtUtc",
            table: "ClassifierRuns",
            column: "StartedAtUtc");

        // The runtime roles' grants on the new tables (RuntimePermissions.csv):
        // the Web opens a run, closes it once and reads it back, and writes a
        // run's messages once with its close; nothing deletes either, and the
        // Worker never touches them.
        migrationBuilder.Sql("""
            GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ClassifierRuns] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[ClassifierRuns] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[ClassifierRuns] TO [pegasus_worker_runtime_role];
            GRANT SELECT, INSERT ON OBJECT::[dbo].[ClassifierRunItems] TO [pegasus_web_runtime_role];
            DENY UPDATE, DELETE ON OBJECT::[dbo].[ClassifierRunItems] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[ClassifierRunItems] TO [pegasus_worker_runtime_role];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClassifierRunItems");
        migrationBuilder.DropTable(name: "ClassifierRuns");
    }
}
