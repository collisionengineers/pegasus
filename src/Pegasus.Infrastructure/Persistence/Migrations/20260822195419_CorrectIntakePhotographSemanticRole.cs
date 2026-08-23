using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // DOCS-009: every accepted intake attachment was recorded as Instruction
    // whatever it was, so a case's own damage photographs were filed as
    // instruction documents. Both the evidence gallery's eligibility column
    // and EVA image selection ask "is this an image?" by semantic role, so on
    // the deployed estate neither could see a single photograph — an export of
    // QDOS26011 would have produced an archive with nothing in it.
    //
    // The code fix only governs cases accepted after it ships. This corrects
    // what is already recorded, so cases created before the fix behave like
    // cases created after it.
    //
    // The two loops in EfQueuedCustodyProcessor stamp distinguishable
    // operation keys — ':attachment:' for a file that arrived attached,
    // ':embedded:' for a photograph found inside a PDF. Matching on that
    // marker is what makes both directions exact: embedded photographs were
    // always correctly Image and must not be touched by either.
    public partial class CorrectIntakePhotographSemanticRole : Migration
    {
        private const string AttachmentPhotographs = """
            FROM [dbo].[DocumentOccurrences] AS o
            INNER JOIN [dbo].[DocumentVersions] AS v ON v.[Id] = o.[VersionId]
            WHERE o.[Source] = N'Intake'
              AND o.[OperationKey] LIKE N'%:attachment:%'
              AND v.[MediaType] LIKE N'image/%'
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                $"""
                UPDATE o SET o.[SemanticRole] = N'Image'
                {AttachmentPhotographs}
                  AND o.[SemanticRole] = N'Instruction';
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                $"""
                UPDATE o SET o.[SemanticRole] = N'Instruction'
                {AttachmentPhotographs}
                  AND o.[SemanticRole] = N'Image';
                """);
    }
}
