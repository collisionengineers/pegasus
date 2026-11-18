using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Estimate document type is split into CE estimate and External estimate,
/// beside the new Repair invoice (operator, 17 November 2026; R1139). The files
/// a Glass's session retained read CE estimate; every other file typed
/// Estimate reads External estimate, the declared type of an upload not yet
/// filed included. Non-additive and forward only (R1153): it rewrites the
/// stored type of those rows, and the application before this migration cannot
/// read the new names. Case history keeps the name each change recorded.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261117200000_EstimateDocumentTypes")]
public partial class EstimateDocumentTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(RetypeSql);
        }
    }

    /// <summary>
    /// The retyping, held apart so a test can run it over rows it seeded. A
    /// Glass's session files its XML export, calculation sheet and rejected
    /// export under <c>glass-estimate:{session}:…</c>
    /// (<c>GlassRepairEstimateGateway</c>), which is what names them here.
    /// </summary>
    internal const string RetypeSql =
        """
        UPDATE [dbo].[DocumentOccurrences]
        SET [SemanticRole] = N'CeEstimate'
        WHERE [SemanticRole] = N'Estimate'
            AND [SourceOccurrenceIdentity] LIKE N'glass-estimate:%';

        UPDATE [dbo].[DocumentOccurrences]
        SET [SemanticRole] = N'ExternalEstimate'
        WHERE [SemanticRole] = N'Estimate';

        UPDATE [dbo].[IntakeReceipts]
        SET [DeclaredDocumentType] = N'ExternalEstimate'
        WHERE [DeclaredDocumentType] = N'Estimate';

        UPDATE [dbo].[IntakeStagedReceipts]
        SET [DeclaredDocumentType] = N'ExternalEstimate'
        WHERE [DeclaredDocumentType] = N'Estimate';
        """;

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Estimate is no longer a document type; the CE estimate and External estimate types are not merged back.");
}
