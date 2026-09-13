using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Place on Hold gains an optional review date (Work Centre D5): the workflow row
/// keeps when the current hold was placed and the Europe/London date the person
/// means to look again, both cleared when the hold is released. Until now only a
/// Not ready hold remembered its moment, on the due-work row, so a Held decision
/// could not age. The row also learns when the Case entered its current state,
/// which the Work Centre's Review and Unassigned rows count from.
///
/// Existing Held Cases take their hold moment from the retained hold event, and
/// every Case takes its most recent workflow event as the state entry it is
/// aged from; a Case with no retained event stays null and readers fall back to
/// its creation.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913083338_CaseHoldReviewDate")]
public partial class CaseHoldReviewDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "HeldAtUtc",
            table: "CaseWorkflows",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "HoldReviewOn",
            table: "CaseWorkflows",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "StateEnteredAtUtc",
            table: "CaseWorkflows",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseWorkflows_HoldReview",
            table: "CaseWorkflows",
            sql: "[HoldReviewOn] IS NULL OR [HeldAtUtc] IS NOT NULL");

        migrationBuilder.Sql(
            """
            UPDATE workflow
            SET HeldAtUtc = held.OccurredAtUtc
            FROM dbo.CaseWorkflows AS workflow
            CROSS APPLY (
                SELECT TOP (1) entry.OccurredAtUtc
                FROM dbo.CaseWorkflowEvents AS entry
                WHERE entry.CaseId = workflow.CaseId
                  AND entry.EventType = N'case_held'
                ORDER BY entry.OccurredAtUtc DESC) AS held
            WHERE workflow.State = N'Held';

            UPDATE workflow
            SET StateEnteredAtUtc = latest.OccurredAtUtc
            FROM dbo.CaseWorkflows AS workflow
            CROSS APPLY (
                SELECT TOP (1) entry.OccurredAtUtc
                FROM dbo.CaseWorkflowEvents AS entry
                WHERE entry.CaseId = workflow.CaseId
                ORDER BY entry.OccurredAtUtc DESC) AS latest;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseWorkflows_HoldReview",
            table: "CaseWorkflows");

        migrationBuilder.DropColumn(name: "HeldAtUtc", table: "CaseWorkflows");
        migrationBuilder.DropColumn(name: "HoldReviewOn", table: "CaseWorkflows");
        migrationBuilder.DropColumn(name: "StateEnteredAtUtc", table: "CaseWorkflows");
    }
}
