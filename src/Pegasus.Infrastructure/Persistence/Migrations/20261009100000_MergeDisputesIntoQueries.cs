using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A dispute is a query (operator, 9 October 2026): the <c>dispute</c>
/// subtype of <c>post-report-emails</c> becomes <c>query</c>, and the Case
/// list's Disputes column goes. The current decision's subtype is rewritten,
/// and so is the subtype inside the correction history's before/after
/// snapshots, because the history reader rebuilds each snapshot through the
/// current taxonomy. A shared Case list preset that ticked Disputes ticks
/// Queries instead, once, because a preset with an unknown column refuses
/// every download. Query counts are unchanged: they already included disputes.
/// Forward-only: which queries were disputes is not recoverable.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261009100000_MergeDisputesIntoQueries")]
public partial class MergeDisputesIntoQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "UPDATE [dbo].[IntakeMailClassificationDecisions] SET [Subtype] = N'query' "
            + "WHERE [Family] = N'post-report-emails' AND [Subtype] = N'dispute';");
        foreach (var column in new[] { "BeforeJson", "AfterJson" })
        {
            migrationBuilder.Sql(
                $"UPDATE [dbo].[IntakeMailClassificationHistory] SET [{column}] = REPLACE([{column}], N'\"subtype\":\"dispute\"', N'\"subtype\":\"query\"') "
                + $"WHERE CHARINDEX(N'\"subtype\":\"dispute\"', [{column}]) > 0;");
        }

        migrationBuilder.Sql(
            """
            UPDATE [preset]
            SET [ColumnKeysJson] = (
                SELECT N'[' + STRING_AGG(CAST(N'"' + STRING_ESCAPE([keys].[ColumnKey], 'json') + N'"' AS nvarchar(max)), N',')
                    WITHIN GROUP (ORDER BY [keys].[Position]) + N']'
                FROM (
                    SELECT CASE WHEN [value] = N'activity.disputes' THEN N'activity.queries' ELSE [value] END AS [ColumnKey],
                        MIN(CAST([key] AS int)) AS [Position]
                    FROM OPENJSON([preset].[ColumnKeysJson])
                    GROUP BY CASE WHEN [value] = N'activity.disputes' THEN N'activity.queries' ELSE [value] END
                ) AS [keys])
            FROM [dbo].[CaseListPresets] AS [preset]
            WHERE CHARINDEX(N'"activity.disputes"', [preset].[ColumnKeysJson]) > 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "MergeDisputesIntoQueries is forward-only: which queries were disputes is not recoverable. Restore from an approved backup instead.");
}
