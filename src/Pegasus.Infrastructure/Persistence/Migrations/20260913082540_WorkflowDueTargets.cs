using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Work Centre gives every kind of work a due instant (Work Centre D3), and
/// the targets that produce those instants are workflow settings beside the
/// chase interval: Unidentified (0 days, so due by the next midnight), Triage
/// (1), Held decision (7, unless the hold carries its own review date), Review
/// (1, with or without an engineer) and AI draft (1). All are calendar days on
/// the 0–365 range the check constraint fixes; the existing chase interval keeps
/// its own 1–365 rule. The single configuration row takes the defaults, so the
/// office sees the standard until it has a reason to change it.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913082540_WorkflowDueTargets")]
public partial class WorkflowDueTargets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AiDraftTargetDays",
            table: "WorkflowConfigurations",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "HeldTargetDays",
            table: "WorkflowConfigurations",
            type: "int",
            nullable: false,
            defaultValue: 7);

        migrationBuilder.AddColumn<int>(
            name: "ReviewTargetDays",
            table: "WorkflowConfigurations",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "TriageTargetDays",
            table: "WorkflowConfigurations",
            type: "int",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<int>(
            name: "UnidentifiedTargetDays",
            table: "WorkflowConfigurations",
            type: "int",
            nullable: false,
            defaultValue: 0);

        // The one configuration row takes the standard targets explicitly, so the
        // seed the model declares and the live row agree. Plain SQL: a hand-written
        // migration carries no model for a typed data operation to read.
        migrationBuilder.Sql(
            """
            UPDATE dbo.WorkflowConfigurations
            SET AiDraftTargetDays = 1, HeldTargetDays = 7, ReviewTargetDays = 1, TriageTargetDays = 1, UnidentifiedTargetDays = 0
            WHERE Id = N'case-workflow';
            """);

        migrationBuilder.AddCheckConstraint(
            name: "CK_WorkflowConfigurations_TargetDays",
            table: "WorkflowConfigurations",
            sql: "[UnidentifiedTargetDays] BETWEEN 0 AND 365 AND [TriageTargetDays] BETWEEN 0 AND 365 AND [HeldTargetDays] BETWEEN 0 AND 365 AND [ReviewTargetDays] BETWEEN 0 AND 365 AND [AiDraftTargetDays] BETWEEN 0 AND 365");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_WorkflowConfigurations_TargetDays",
            table: "WorkflowConfigurations");

        migrationBuilder.DropColumn(name: "AiDraftTargetDays", table: "WorkflowConfigurations");
        migrationBuilder.DropColumn(name: "HeldTargetDays", table: "WorkflowConfigurations");
        migrationBuilder.DropColumn(name: "ReviewTargetDays", table: "WorkflowConfigurations");
        migrationBuilder.DropColumn(name: "TriageTargetDays", table: "WorkflowConfigurations");
        migrationBuilder.DropColumn(name: "UnidentifiedTargetDays", table: "WorkflowConfigurations");
    }
}
