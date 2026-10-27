using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A received message has one of three categories, or none (operator, 27 October
/// 2026). The decision gains <c>OpensQuery</c>. A stored category that is no
/// longer a category becomes Unclassified with no category, and so does each
/// correction-history snapshot that names one, because the history reader
/// rebuilds every snapshot through the current categories. Existing CaseQueries
/// rows are left alone. Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261027160000_ThreeMailCategories")]
public partial class ThreeMailCategories : Migration
{
    private const string RemovedCategories =
        "N'pre-instruction', N'images-received', N'case-update', N'query', N'billing', N'general', N'not-client-related', N'internal-cc'";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "OpensQuery",
            table: "IntakeMailClassificationDecisions",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql(
            $"""
            UPDATE [dbo].[IntakeMailClassificationDecisions]
            SET [Outcome] = N'unclassified', [Category] = NULL, [CaseType] = NULL
            WHERE [Category] IN ({RemovedCategories});
            """);

        // The snapshots are camel-case JSON. Outcome is the enum number:
        // Classified 0, Unclassified 1. A removed category code cannot be reread.
        foreach (var column in new[] { "BeforeJson", "AfterJson" })
        {
            migrationBuilder.Sql(
                $"""
                UPDATE [dbo].[IntakeMailClassificationHistory]
                SET [{column}] = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY([{column}], '$.outcome', 1), '$.category', NULL), '$.caseType', NULL)
                WHERE JSON_VALUE([{column}], '$.category') IN ({RemovedCategories});
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "20261027160000_ThreeMailCategories is forward-only: removed categories are not restored.");
}
