using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Market research is a document type, not an image tag (operator, 7 October
/// 2026). Every findings file filed by a Market research AI job, whether it
/// wears the built-in "Market research" tag or not, takes the MarketResearch
/// semantic role. The tag then comes off every occurrence and out of the image
/// tag vocabulary, so it is no longer offered on photographs.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007140000_MarketResearchDocumentRole")]
public partial class MarketResearchDocumentRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            UPDATE occurrence
            SET SemanticRole = N'MarketResearch'
            FROM dbo.DocumentOccurrences AS occurrence
            WHERE occurrence.SourceOccurrenceIdentity LIKE N'ai-market-research:%'
               OR EXISTS (
                   SELECT 1
                   FROM dbo.DocumentOccurrenceTags AS tag
                   WHERE tag.OccurrenceId = occurrence.Id
                     AND tag.TagId = N'00000000-0000-4000-8000-0000000017a5');

            DELETE FROM dbo.DocumentOccurrenceTags WHERE TagId = N'00000000-0000-4000-8000-0000000017a5';
            DELETE FROM dbo.ImageTags WHERE Id = N'00000000-0000-4000-8000-0000000017a5';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.ImageTags WHERE Id = N'00000000-0000-4000-8000-0000000017a5')
            INSERT dbo.ImageTags (Id, Name, NormalizedName, Colour, IsBuiltIn, CreatedAtUtc, CreatedBy, CreateOperationKey, Version)
            VALUES (N'00000000-0000-4000-8000-0000000017a5', N'Market research', N'MARKET RESEARCH', N'Grey', 1, SYSDATETIMEOFFSET(), N'system', N'image-tag-seed:market-research', 1);

            INSERT dbo.DocumentOccurrenceTags (OccurrenceId, TagId, AppliedByKind, AppliedBySubjectId, AppliedAtUtc, OperationKey)
            SELECT occurrence.Id, N'00000000-0000-4000-8000-0000000017a5', N'Automation', N'system', occurrence.RecordedAtUtc, occurrence.OperationKey
            FROM dbo.DocumentOccurrences AS occurrence
            WHERE occurrence.SemanticRole = N'MarketResearch';

            UPDATE dbo.DocumentOccurrences SET SemanticRole = N'Other' WHERE SemanticRole = N'MarketResearch';
            """);
    }
}
