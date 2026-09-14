using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Draft ready is a Needs attention kind (Work Centre D9) and ages under the AI
/// draft target, so the job row keeps the moment the client wrote its draft.
/// Existing Draft ready jobs take it from the retained draft-ready history
/// entry; a job with none stays null and readers fall back to when it was taken.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913092816_AiJobDraftReadyAt")]
public partial class AiJobDraftReadyAt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DraftReadyAtUtc",
            table: "AiJobs",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE job
            SET DraftReadyAtUtc = ready.OccurredAtUtc
            FROM dbo.AiJobs AS job
            CROSS APPLY (
                SELECT TOP (1) entry.OccurredAtUtc
                FROM dbo.ActionHistory AS entry
                WHERE entry.AggregateType = N'ai_job'
                  AND entry.AggregateId = CONVERT(nvarchar(36), job.JobId)
                  AND entry.EventKind = N'ai_job_draft_ready'
                ORDER BY entry.OccurredAtUtc DESC) AS ready;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DraftReadyAtUtc", table: "AiJobs");
    }
}
