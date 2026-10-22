using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Estimate and Valuation Evidence join the document types, and Market research
/// becomes part of Valuation Evidence (operator, 22 October 2026). Documents
/// filed before are re-tagged by their source identity: an estimate import
/// still Other and a Glass's session file still Original source become
/// Estimate; a guide valuation report still Other and every Market research
/// document become Valuation Evidence. A document staff marked as the original
/// report keeps Audit report. Forward-only: a re-tagged row cannot be told
/// apart from one filed with its new type.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261022120000_DocumentTypesEstimateAndValuationEvidence")]
public class DocumentTypesEstimateAndValuationEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        // One pass over the occurrences; only the four rules' rows change.
        migrationBuilder.Sql("""
            UPDATE [dbo].[DocumentOccurrences]
            SET [SemanticRole] = CASE
                WHEN [SemanticRole] = N'MarketResearch'
                  OR [SourceOccurrenceIdentity] LIKE N'guide-valuation-report:%' THEN N'ValuationEvidence'
                ELSE N'Estimate'
            END
            WHERE ([SemanticRole] = N'Other' AND [SourceOccurrenceIdentity] LIKE N'estimate-import:%')
               OR ([SemanticRole] = N'OriginalSource' AND [SourceOccurrenceIdentity] LIKE N'glass-estimate:%')
               OR ([SemanticRole] = N'Other' AND [SourceOccurrenceIdentity] LIKE N'guide-valuation-report:%')
               OR [SemanticRole] = N'MarketResearch';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "DocumentTypesEstimateAndValuationEvidence is forward-only: filed documents were re-tagged Estimate or Valuation Evidence and the Market research type was removed. Restore from an approved backup instead.");
}
