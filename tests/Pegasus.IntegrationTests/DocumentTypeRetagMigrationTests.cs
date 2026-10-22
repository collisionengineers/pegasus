using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261022120000_DocumentTypesEstimateAndValuationEvidence: documents filed
/// before Estimate and Valuation Evidence existed are re-tagged by their source
/// identity (operator, 22 October 2026). A document staff marked as the
/// original report, and every other filing, keep their type.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DocumentTypeRetagMigrationTests
{
    private const string PreviousMigration = "20261021120000_GrantWebRepairSpecDiscardDelete";
    private const string Migration = "20261022120000_DocumentTypesEstimateAndValuationEvidence";
    private const string CaseId = RepairSpecInUseOnCreateMigrationTests.CaseId;
    private const string Session = "5e551000-0000-0000-0000-000000000001";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    // Each filed occurrence: its source identity, the type and source it was
    // filed with, and the type it has after the migration.
    private static readonly (string Identity, string Before, string Source, string After)[] Filed =
    [
        ("estimate-import:mcp-1", "Other", "StaffUpload", "Estimate"),
        ("estimate-import:mcp-2", "AuditReport", "StaffUpload", "AuditReport"),
        ($"glass-estimate:{Session}:xml", "OriginalSource", "Generated", "Estimate"),
        ($"glass-estimate:{Session}:pdf", "OriginalSource", "Generated", "Estimate"),
        ($"glass-estimate:{Session}:rejected-xml", "OriginalSource", "Generated", "Estimate"),
        ("guide-valuation-report:Glasses:glass-stock:1", "Other", "Generated", "ValuationEvidence"),
        ("guide-valuation-report:Glasses:glass-stock:2", "AuditReport", "Generated", "AuditReport"),
        ("ai-market-research:5e551000-0000-0000-0000-000000000002", "MarketResearch", "Automation", "ValuationEvidence"),
        ("automation:mcp:research", "MarketResearch", "Automation", "ValuationEvidence"),
        ("5e5510000000000000000000000000aa", "OriginalSource", "Intake", "OriginalSource"),
        ("5e5510000000000000000000000000ab", "Image", "Intake", "Image"),
        ("automation:mcp:note", "Other", "Automation", "Other")
    ];

    [Fact]
    public async Task FiledDocumentsAreRetaggedBySourceIdentityAndTheMigrationCannotBeReverted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(string.Concat(Filed.Select((row, index) => FiledSql(index + 1, row))));

        await context.Database.MigrateAsync(Migration);

        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        // The types in filing order, one per row of Filed.
        Assert.Equal(
            string.Join(", ", Filed.Select(row => row.After)),
            await database.ScalarAsync<string>(
                $"SELECT STRING_AGG(SemanticRole, N', ') WITHIN GROUP (ORDER BY Ordinal) FROM DocumentOccurrences WHERE CaseId = '{CaseId}'"));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(PreviousMigration));
        Assert.StartsWith(
            "DocumentTypesEstimateAndValuationEvidence is forward-only",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Equal(Migration, (await context.Database.GetAppliedMigrationsAsync()).Last());
    }

    private static string FiledSql(int ordinal, (string Identity, string Before, string Source, string After) row) =>
        $"""
        INSERT INTO CaseDocuments (Id, CaseId, Ordinal, SourceOccurrenceIdentity)
        VALUES ('d1000000-0000-0000-0000-{ordinal:D12}', '{CaseId}', {ordinal}, N'{row.Identity}');

        INSERT INTO DocumentVersions
            (Id, DocumentId, Version, FileName, MediaType, ContentLength, Sha256, CustodyStatus,
             CreatedAtUtc, CreatedBy, IsCurrent, IsLogicallyRemoved)
        VALUES
            ('d2000000-0000-0000-0000-{ordinal:D12}', 'd1000000-0000-0000-0000-{ordinal:D12}', 1,
             N'filed-{ordinal}.pdf', N'application/pdf', 4, REPLICATE(N'a', 64), N'Confirmed',
             '{Recorded}', N'migration', 1, 0);

        INSERT INTO DocumentOccurrences
            (Id, CaseId, DocumentId, VersionId, Ordinal, SemanticRole, Source, SourceOccurrenceIdentity,
             RecordedAtUtc, OperationKey, InReport, RotationDegrees, PreparationVersion, PreparationFullPage)
        VALUES
            ('d3000000-0000-0000-0000-{ordinal:D12}', '{CaseId}', 'd1000000-0000-0000-0000-{ordinal:D12}',
             'd2000000-0000-0000-0000-{ordinal:D12}', {ordinal}, N'{row.Before}', N'{row.Source}',
             N'{row.Identity}', '{Recorded}', N'retag-{ordinal}', 1, 0, 0, 0);

        """;
}
