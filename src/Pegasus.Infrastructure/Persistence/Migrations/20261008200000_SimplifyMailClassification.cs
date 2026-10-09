using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Simplifies mail classification (operator, 8 October 2026; ADR-0067). Sent
/// categories, the Other category and the Ambiguous outcome go: a decision
/// that carried any of them becomes Unclassified, in the current row and in
/// the correction history's snapshots, whose reader rebuilds every snapshot
/// through the current taxonomy. The history snapshots store enums as numbers
/// (System.Text.Json web defaults), and removing Ambiguous renumbers
/// Unclassified from 2 to 1. An Unidentified item recorded for an ambiguous
/// destination becomes "no usable identification". The Outlook folder-move
/// pipeline's two tables go with it. SQL Server drops a table's permission
/// rows with the table, so it needs no REVOKE. Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261008200000_SimplifyMailClassification")]
public partial class SimplifyMailClassification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            "UPDATE [dbo].[IntakeMailClassificationDecisions] SET [Outcome] = N'unclassified', [Family] = NULL, [Subtype] = NULL, "
            + "[CaseType] = NULL, [IsReplyContext] = 0, [StandaloneAuditReportAssetSourceLabel] = NULL, [StandaloneAuditReportAssessment] = NULL "
            + "WHERE [Outcome] = N'ambiguous' OR [OtherName] IS NOT NULL OR [Direction] = N'sent';");

        // Classified is 0 before and after; Ambiguous (1) and Unclassified (2)
        // both become the new Unclassified (1). A null value removes the key,
        // which the reader takes as null.
        foreach (var column in new[] { "BeforeJson", "AfterJson" })
        {
            migrationBuilder.Sql(
                $"UPDATE [dbo].[IntakeMailClassificationHistory] SET [{column}] = "
                + $"JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([{column}], '$.outcome', 1), "
                + "'$.family', NULL), '$.subtype', NULL), '$.caseType', NULL), '$.standaloneAuditReport', NULL) "
                + $"WHERE ISJSON([{column}]) = 1 AND (JSON_VALUE([{column}], '$.outcome') <> '0' "
                + $"OR JSON_VALUE([{column}], '$.direction') = '1' OR JSON_VALUE([{column}], '$.otherName') IS NOT NULL);");
        }

        migrationBuilder.Sql(
            "UPDATE [dbo].[UnidentifiedItems] SET [ReasonCode] = N'NoUsableIdentification' "
            + "WHERE [ReasonCode] = N'AmbiguousOwnershipOrDestination';");

        migrationBuilder.DropColumn(
            name: "Direction",
            table: "IntakeMailClassificationDecisions");
        migrationBuilder.DropColumn(
            name: "OtherName",
            table: "IntakeMailClassificationDecisions");
        migrationBuilder.DropColumn(
            name: "OtherReasoning",
            table: "IntakeMailClassificationDecisions");
        migrationBuilder.DropColumn(
            name: "AmbiguousCandidatesJson",
            table: "IntakeMailClassificationDecisions");

        migrationBuilder.DropTable(
            name: "RetainedMailFolderMoves");
        migrationBuilder.DropTable(
            name: "ApprovedMailboxFolderBindings");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "SimplifyMailClassification is forward-only: it drops the Sent, Other and Ambiguous classification facts and the folder-move tables. Restore from an approved backup instead.");
}
