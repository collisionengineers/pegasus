using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729172000_CaseHoldState")]
public partial class CaseHoldState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreHoldState",
            table: "CaseWorkflows",
            maxLength: 40,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE CaseWorkflows
            SET State = 'ReportPreparation'
            WHERE State = 'Active';
            """);

        migrationBuilder.Sql(
            """
            UPDATE CaseWorkflows
            SET PreHoldState = 'NotReady'
            WHERE State = 'Held'
              AND EXISTS (
                  SELECT 1
                  FROM CaseDueWork due
                  WHERE due.CaseId = CaseWorkflows.CaseId
                    AND due.State = 'Held'
              );
            """);

        migrationBuilder.Sql(
            """
            UPDATE CaseWorkflows
            SET PreHoldState = (
                SELECT CASE
                    WHEN prior.EventType IN ('case_hold_released', 'case_reopened_NotReady', 'manual_chase_recorded') THEN 'NotReady'
                    WHEN prior.EventType IN ('case_returned_to_review', 'case_engineer_assigned', 'case_reopened_Review') THEN 'Review'
                    WHEN prior.EventType IN ('state_Active', 'state_ReportPreparation', 'case_report_approved', 'case_reopened_Active', 'case_reopened_ReportPreparation') THEN 'ReportPreparation'
                    WHEN prior.EventType IN ('case_report_sent', 'case_reopened_PostReport') THEN 'PostReport'
                    ELSE NULL
                END
                FROM CaseWorkflowEvents held
                INNER JOIN CaseWorkflowEvents prior
                    ON prior.CaseId = held.CaseId
                   AND prior.AfterVersion = held.BeforeVersion
                WHERE held.CaseId = CaseWorkflows.CaseId
                  AND held.EventType = 'case_held'
                  AND held.AfterVersion = CaseWorkflows.Version
            )
            WHERE State = 'Held'
              AND PreHoldState IS NULL;
            """);

        migrationBuilder.Sql(
            """
            UPDATE CaseWorkflows
            SET PreHoldState = (
                SELECT CASE accepted.InitialState
                    WHEN 'not_ready' THEN 'NotReady'
                    WHEN 'review' THEN 'Review'
                    ELSE NULL
                END
                FROM Cases accepted
                WHERE accepted.Id = CaseWorkflows.CaseId
            )
            WHERE State = 'Held'
              AND PreHoldState IS NULL
              AND EXISTS (
                  SELECT 1
                  FROM CaseWorkflowEvents held
                  WHERE held.CaseId = CaseWorkflows.CaseId
                    AND held.EventType = 'case_held'
                    AND held.AfterVersion = CaseWorkflows.Version
                    AND held.BeforeVersion = 0
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PreHoldState",
            table: "CaseWorkflows");
    }
}
