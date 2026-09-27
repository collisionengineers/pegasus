using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20260927004150_ImageInReport: whether the report uses an image is a flag
/// (operator, 26 September 2026). Every existing image keeps whether the
/// report used it: a Close-up, Overview or Supporting role is in, Not used or
/// no role is out.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ImageInReportMigrationTests
{
    private const string CaseWorksBaseline = "20260924090000_IntakeAssetBoxParentFolder";
    private const string PreviousMigration = "20260926150000_DeclaredUploadDestination";
    private const string CaseId = "a7000000-0000-0000-0000-000000000001";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    private static readonly (int Ordinal, string? Role, bool InReport)[] Images =
    [
        (1, "CloseUp", true),
        (2, "Overview", true),
        (3, "Supporting", true),
        (4, "NotUsed", false),
        (5, null, false),
    ];

    [Fact]
    public async Task EveryImageKeepsWhetherTheReportUsedIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(CaseWorksBaseline);
        await database.ExecuteAsync(CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(ImagesSql());

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        foreach (var (ordinal, _, inReport) in Images)
        {
            Assert.Equal(inReport ? 1 : 0, await database.ScalarAsync<int>(
                $"SELECT CONVERT(int, InReport) FROM DocumentOccurrences WHERE CaseId = '{CaseId}' AND Ordinal = {ordinal}"));
        }
    }

    private const string CaseSql =
        $"""
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('a7000000-0000-0000-0000-000000000011', N'Image in report migration provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('a7000000-0000-0000-0000-000000000012', '{Recorded}');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
        VALUES
            ('a7000000-0000-0000-0000-000000000013', 'a7000000-0000-0000-0000-000000000011', N'IIRM',
             'a7000000-0000-0000-0000-000000000012', NULL, NULL, 1, 0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{CaseId}', 'a7000000-0000-0000-0000-000000000013', 'a7000000-0000-0000-0000-000000000012',
             2031, 1, N'IIRM31001', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID());
        """;

    /// <summary>One image per role the retired column held, each its own document and version.</summary>
    private static string ImagesSql()
    {
        var sql = new StringBuilder();
        foreach (var (ordinal, role, _) in Images)
        {
            var documentId = $"a7000000-0000-0000-0001-{ordinal:D12}";
            var versionId = $"a7000000-0000-0000-0002-{ordinal:D12}";
            var occurrenceId = $"a7000000-0000-0000-0003-{ordinal:D12}";
            var preparationRole = role is null ? "NULL" : $"N'{role}'";
            sql.AppendLine(CultureInfo.InvariantCulture,
                $"""
                INSERT INTO CaseDocuments (Id, CaseId, Ordinal, SourceOccurrenceIdentity)
                VALUES ('{documentId}', '{CaseId}', {ordinal}, N'image:{ordinal}');

                INSERT INTO DocumentVersions
                    (Id, DocumentId, Version, FileName, MediaType, ContentLength, Sha256, CustodyStatus,
                     CreatedAtUtc, CreatedBy, IsCurrent, IsLogicallyRemoved)
                VALUES
                    ('{versionId}', '{documentId}', 1, N'image-{ordinal}.jpg', N'image/jpeg', 1, REPLICATE(N'a', 64),
                     N'Confirmed', '{Recorded}', N'Staff:test', 1, 0);

                INSERT INTO DocumentOccurrences
                    (Id, CaseId, DocumentId, VersionId, Ordinal, SemanticRole, Source, SourceOccurrenceIdentity,
                     RecordedAtUtc, OperationKey, PreparationRole, PreparationFullPage, PreparationVersion, RotationDegrees)
                VALUES
                    ('{occurrenceId}', '{CaseId}', '{documentId}', '{versionId}', {ordinal}, N'Image', N'StaffUpload',
                     N'image:{ordinal}', '{Recorded}', N'seed-image:{ordinal}', {preparationRole}, 0, 0, 0);
                """);
        }
        return sql.ToString();
    }
}
