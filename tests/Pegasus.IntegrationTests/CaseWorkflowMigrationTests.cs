using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
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
             'migration_test_reader', '1', 'migration_test_policy', 1, 'draft_ready', 'Ready for review',
             '{"version":1,"data":[]}', '{"version":1,"data":[]}', NULL, NULL,
             '{"version":1,"data":[]}'),
            ('50000000-0000-0000-0000-000000000002', 'not-ready.eml', 'message/rfc822', 1,
             'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB', 'manual_upload',
             'workflow-migration-not-ready', '2031-05-06T10:31:00+00:00', '2031-05-06T10:31:00+00:00',
             'migration_test_reader', '1', 'migration_test_policy', 1, 'draft_ready', 'Missing images',
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
