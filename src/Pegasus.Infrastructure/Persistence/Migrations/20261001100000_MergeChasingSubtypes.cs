using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The two chasing subtypes of <c>in-progress-cases</c> become one,
/// <c>chasing-for-update</c>, because the operator reads them as one category
/// (1 October 2026). The current decision's subtype is rewritten, and so is the
/// token inside the correction history's before/after snapshots: the history
/// reader rebuilds each snapshot through the current taxonomy, so a retired
/// name there would stop the message's classification from loading. The
/// recorded fact (who corrected what, when and why) is unchanged.
/// Forward-only: the client/Principal split is not recoverable.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261001100000_MergeChasingSubtypes")]
public partial class MergeChasingSubtypes : Migration
{
    private static readonly string[] RetiredSubtypes = ["client-chasing-for-update", "principal-chasing-for-update"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var retired in RetiredSubtypes)
        {
            migrationBuilder.Sql(
                $"UPDATE [dbo].[IntakeMailClassificationDecisions] SET [Subtype] = N'chasing-for-update' WHERE [Subtype] = N'{retired}';");
            foreach (var column in new[] { "BeforeJson", "AfterJson" })
            {
                migrationBuilder.Sql(
                    $"UPDATE [dbo].[IntakeMailClassificationHistory] SET [{column}] = REPLACE([{column}], N'\"{retired}\"', N'\"chasing-for-update\"') "
                    + $"WHERE CHARINDEX(N'\"{retired}\"', [{column}]) > 0;");
            }
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "MergeChasingSubtypes is forward-only: the client/Principal chasing split is not recoverable. Restore from an approved backup instead.");
}
