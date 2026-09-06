using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseWorkflowMigrationTests
{
    private const string PreviousMigration = "20260729152105_WorkflowTriageEmailEvidence";
    private const string WorkflowMigration = "20260729160000_CaseWorkflowRuntime";
    private const string ReviewCaseId = "60000000-0000-0000-0000-000000000001";
    private const string NotReadyCaseId = "60000000-0000-0000-0000-000000000002";

    [Fact]
    public async Task SqlServerUpgradeBackfillsExistingReviewAndNotReadyCasesWithRequiredTokens()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(ExistingCasesSql);
        await context.Database.MigrateAsync(WorkflowMigration);

        Assert.Equal(2, await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseWorkflows"));
        Assert.Equal(
            "Review",
            await database.ScalarAsync<string>(
                $"SELECT State FROM CaseWorkflows WHERE CaseId='{ReviewCaseId}'"));
        Assert.Equal(
            "NotReady",
            await database.ScalarAsync<string>(
                $"SELECT State FROM CaseWorkflows WHERE CaseId='{NotReadyCaseId}'"));
        AssertNonEmptyGuid(await database.ScalarAsync<Guid>(
            $"SELECT ConcurrencyToken FROM CaseWorkflows WHERE CaseId='{ReviewCaseId}'"));
        AssertNonEmptyGuid(await database.ScalarAsync<Guid>(
            $"SELECT ConcurrencyToken FROM CaseWorkflows WHERE CaseId='{NotReadyCaseId}'"));

        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseDueWork"));
        Assert.Equal(
            "Scheduled",
            await database.ScalarAsync<string>(
                $"SELECT State FROM CaseDueWork WHERE CaseId='{NotReadyCaseId}'"));
        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                $"SELECT CASE WHEN NextChaseAtUtc IS NULL THEN 0 ELSE 1 END FROM CaseDueWork WHERE CaseId='{NotReadyCaseId}'"));
        AssertNonEmptyGuid(await database.ScalarAsync<Guid>(
            $"SELECT ConcurrencyToken FROM CaseDueWork WHERE CaseId='{NotReadyCaseId}'"));
    }

    [Fact]
    public void WorkflowUpgradeScriptUsesSqlServerNativeRequiredTokens()
    {
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=PegasusMigrationGuard;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        using var context = new PegasusDbContext(options);

        var script = context.GetService<IMigrator>().GenerateScript(
            PreviousMigration,
            WorkflowMigration);

        Assert.Contains(
            "INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "(CaseId, MissingMaterialReason, State, NextChaseAtUtc, Version, ConcurrencyToken)",
            script,
            StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(script, "NEWID()"));
    }

    [Fact]
    public async Task CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss()
    {
        const string previous = "20260811063940_QdosAllocationRecovery";
        const string documentId = "70000000-0000-0000-0000-000000000001";
        const string versionId = "71000000-0000-0000-0000-000000000001";
        const string occurrenceId = "72000000-0000-0000-0000-000000000001";
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(previous);
        await database.ExecuteAsync(ExistingCasesSql);
        await database.ExecuteAsync(
            $"""
            INSERT INTO CaseDocuments (Id, CaseId, SourceOccurrenceIdentity)
            VALUES ('{documentId}', '{ReviewCaseId}', 'migration:evidence');
            INSERT INTO DocumentVersions
                (Id, DocumentId, Version, FileName, MediaType, ContentLength, Sha256,
                 CustodyStatus, CreatedAtUtc, CreatedBy, IsCurrent, IsLogicallyRemoved)
            VALUES
                ('{versionId}', '{documentId}', 1, 'evidence.jpg', 'image/jpeg', 1,
                 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
                 'Confirmed', '2031-05-06T10:30:00+00:00', 'migration', 1, 0);
            INSERT INTO DocumentOccurrences
                (Id, CaseId, DocumentId, VersionId, SemanticRole, Source,
                 SourceOccurrenceIdentity, RecordedAtUtc, OperationKey)
            VALUES
                ('{occurrenceId}', '{ReviewCaseId}', '{documentId}', '{versionId}',
                 'Image', 'StaffUpload', 'migration:evidence',
                 '2031-05-06T10:30:00+00:00', 'migration:evidence');
            """);

        // This fixture proves the historical custody/EVA chain. The v1
        // foundation establishes the documented estate on disposable data;
        // it does not convert this pre-release fixture into that estate.
        await context.Database.MigrateAsync("20260905010654_CaseSignOffEngineer");

        Assert.Equal(documentId, await database.ScalarAsync<string>(
            $"SELECT CONVERT(varchar(36), Id) FROM CaseDocuments WHERE Id = '{documentId}'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT Ordinal FROM CaseDocuments WHERE Id = '{documentId}'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"SELECT Ordinal FROM DocumentOccurrences WHERE Id = '{occurrenceId}'"));
        // ENG-016 dropped the hand-off's three tables. This case still
        // proves the migration chain runs to completion over pre-existing
        // rows; the table it used to look for is the evidence that it did.
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'EvaHandoffDownloadOperations'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'EvaFirstHandoffProxies'"));
        Assert.Equal(
            ["20260906054658_V1PlatformFoundation"],
            await context.Database.GetPendingMigrationsAsync());
    }

    private const string ExistingCasesSql =
        """
        INSERT INTO IntakeReceipts
            (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
             ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
             SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Decision,
             DecisionReason, EvidenceJson, FieldsJson, FailureCode, FailureReason, OcrCandidatesJson)
        VALUES
            ('50000000-0000-0000-0000-000000000001', 'review.eml', 'message/rfc822', 1,
             'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA', 'manual_upload',
             'workflow-migration-review', '2031-05-06T10:30:00+00:00', '2031-05-06T10:30:00+00:00',
             'migration_test_reader', '1', 'migration_test_policy', 1, 'case_created', 'Ready for review',
             '{"version":1,"data":[]}', '{"version":1,"data":[]}', NULL, NULL,
             '{"version":1,"data":[]}'),
            ('50000000-0000-0000-0000-000000000002', 'not-ready.eml', 'message/rfc822', 1,
             'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB', 'manual_upload',
             'workflow-migration-not-ready', '2031-05-06T10:31:00+00:00', '2031-05-06T10:31:00+00:00',
             'migration_test_reader', '1', 'migration_test_policy', 1, 'case_created', 'Missing images',
             '{"version":1,"data":[]}', '{"version":1,"data":[]}', NULL, NULL,
             '{"version":1,"data":[]}');

        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('20000000-0000-0000-0000-000000000001', 'Workflow migration provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('30000000-0000-0000-0000-000000000001', '2031-05-06T10:30:00+00:00');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
        VALUES
            ('40000000-0000-0000-0000-000000000001',
             '20000000-0000-0000-0000-000000000001',
             'QDOS',
             '30000000-0000-0000-0000-000000000001',
             NULL,
             NULL,
             1,
             0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete,
             InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version,
             ConcurrencyToken)
        VALUES
            ('60000000-0000-0000-0000-000000000001',
             '40000000-0000-0000-0000-000000000001',
             '30000000-0000-0000-0000-000000000001',
             2031,
             1,
             'QDOS31001',
             'inspection',
             'review',
             'pending',
             '50000000-0000-0000-0000-000000000001',
             1,
             1,
             1,
             1,
             '2031-05-06T10:30:00+00:00',
             0,
             '70000000-0000-0000-0000-000000000001'),
            ('60000000-0000-0000-0000-000000000002',
             '40000000-0000-0000-0000-000000000001',
             '30000000-0000-0000-0000-000000000001',
             2031,
             2,
             'QDOS31002',
             'inspection',
             'not_ready',
             'pending',
             '50000000-0000-0000-0000-000000000002',
             1,
             0,
             1,
             1,
             '2031-05-06T10:31:00+00:00',
             0,
             '70000000-0000-0000-0000-000000000002');
        """;

    private static void AssertNonEmptyGuid(Guid value)
    {
        Assert.NotEqual(Guid.Empty, value);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
    }

}
