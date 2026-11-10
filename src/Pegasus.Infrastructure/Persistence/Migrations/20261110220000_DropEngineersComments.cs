using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Engineer's comments are the Engineer's Comments block's wording, with no
/// separate field (operator, 10 November 2026; R922). Every stored
/// <c>narrative.engineers_comments</c> value is deleted, not carried into the
/// block. Forward only: the values are not kept. A frozen report keeps what it
/// printed, and the <c>case_assessment_saved</c> history is untouched.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261110220000_DropEngineersComments")]
public partial class DropEngineersComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DELETE FROM [CaseAssessmentFields]
            WHERE [FieldPath] = N'narrative.engineers_comments';
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The Engineer's comments field is retired; its deleted values are not kept.");
}
