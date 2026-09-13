using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the built-in "Market research" tag (Work Centre D9): the findings
/// file a Market research AI job hands back lands in the Case's Files as
/// ordinary evidence wearing this tag, so it reads as research beside the
/// photographs without a review step and without being taken as a value. The
/// identifier is fixed like the other four so Core's vocabulary and the
/// completion store name the same row. Idempotent: a row already present
/// under this identifier is left alone.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260913090000_MarketResearchImageTag")]
public partial class MarketResearchImageTag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.ImageTags WHERE Id = N'00000000-0000-4000-8000-0000000017a5')
            INSERT dbo.ImageTags (Id, Name, NormalizedName, Colour, IsBuiltIn, CreatedAtUtc, CreatedBy, CreateOperationKey, Version)
            VALUES (N'00000000-0000-4000-8000-0000000017a5', N'Market research', N'MARKET RESEARCH', N'Grey', 1, SYSDATETIMEOFFSET(), N'system', N'image-tag-seed:market-research', 1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DELETE FROM dbo.DocumentOccurrenceTags WHERE TagId = N'00000000-0000-4000-8000-0000000017a5';
            DELETE FROM dbo.ImageTags WHERE Id = N'00000000-0000-4000-8000-0000000017a5';
            """);
    }
}
