using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261027160000_ThreeMailCategories: a received message has one of three
/// categories, or none. A stored category that is no longer a category becomes
/// Unclassified, and so does each correction-history snapshot that names one.
/// OpensQuery is added. Existing CaseQueries rows are not rewritten.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ThreeMailCategoriesMigrationTests
{
    private const string PreviousMigration = "20261027140000_AssignableCaseQueries";
    private const string Recorded = "2031-05-06T10:30:00+00:00";
    private const string KeptReceipt = "b9000000-0000-0000-0000-000000000001";
    private const string RemovedReceipt = "b9000000-0000-0000-0000-000000000002";
    private const string HistoryReceipt = "b9000000-0000-0000-0000-000000000003";

    [Fact]
    public async Task MigrationRewritesRemovedCategoriesAndLeavesQueriesAlone()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SeedSql());
        var queriesBefore = await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseQueries");

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM sys.columns
            WHERE object_id = OBJECT_ID(N'IntakeMailClassificationDecisions') AND name = N'OpensQuery'
            """));
        Assert.Equal(
            "classified/new-instruction/0",
            await database.ScalarAsync<string>(
                $"SELECT CONCAT(Outcome, N'/', Category, N'/', OpensQuery) FROM IntakeMailClassificationDecisions WHERE IntakeReceiptId = '{KeptReceipt}'"));
        Assert.Equal(
            "unclassified/-/0",
            await database.ScalarAsync<string>(
                $"SELECT CONCAT(Outcome, N'/', COALESCE(Category, N'-'), N'/', OpensQuery) FROM IntakeMailClassificationDecisions WHERE IntakeReceiptId = '{RemovedReceipt}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM IntakeMailClassificationDecisions WHERE IntakeReceiptId = '{RemovedReceipt}' AND CaseType IS NULL"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM IntakeMailClassificationHistory
            WHERE IntakeReceiptId = '{HistoryReceipt}'
              AND JSON_VALUE(BeforeJson, '$.outcome') = N'1'
              AND JSON_VALUE(BeforeJson, '$.category') IS NULL
              AND JSON_VALUE(BeforeJson, '$.caseType') IS NULL
              AND JSON_VALUE(BeforeJson, '$.policyKey') = N'migration-test'
              AND JSON_VALUE(AfterJson, '$.outcome') = N'0'
              AND JSON_VALUE(AfterJson, '$.category') = N'cancellation'
            """));
        Assert.Equal(queriesBefore, await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseQueries"));
    }

    private static string SeedSql() =>
        $"""
        INSERT INTO IntakeReceipts
            (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken,
             ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision,
             DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson)
        VALUES
            ('{KeptReceipt}', N'kept.eml', N'message/rfc822', 1, REPLICATE(N'1', 64), N'mailbox',
             N'three-categories-kept', '{Recorded}', '{Recorded}', N'migration-test', N'1', 0,
             N'needs_sorting', N'Kept.', N'{{"version":1,"data":[]}}', N'{{"version":1,"data":[]}}',
             N'{{"version":1,"data":[]}}'),
            ('{RemovedReceipt}', N'removed.eml', N'message/rfc822', 1, REPLICATE(N'2', 64), N'mailbox',
             N'three-categories-removed', '{Recorded}', '{Recorded}', N'migration-test', N'1', 0,
             N'needs_sorting', N'Removed.', N'{{"version":1,"data":[]}}', N'{{"version":1,"data":[]}}',
             N'{{"version":1,"data":[]}}'),
            ('{HistoryReceipt}', N'history.eml', N'message/rfc822', 1, REPLICATE(N'3', 64), N'mailbox',
             N'three-categories-history', '{Recorded}', '{Recorded}', N'migration-test', N'1', 0,
             N'needs_sorting', N'History.', N'{{"version":1,"data":[]}}', N'{{"version":1,"data":[]}}',
             N'{{"version":1,"data":[]}}');

        INSERT INTO IntakeMailClassificationDecisions
            (IntakeReceiptId, Outcome, Category, CaseType, IsReplyContext, PredicatesJson, Reason,
             PolicyKey, PolicyVersion, DecidedByActor, DecidedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{KeptReceipt}', N'classified', N'new-instruction', N'inspection', 0,
             N'{{"version":1,"data":[]}}', N'Kept.', N'migration-test', 1, N'test', '{Recorded}', 1, NEWID()),
            ('{RemovedReceipt}', N'classified', N'query', N'inspection', 0,
             N'{{"version":1,"data":[]}}', N'Removed.', N'migration-test', 1, N'test', '{Recorded}', 1, NEWID()),
            ('{HistoryReceipt}', N'classified', N'cancellation', NULL, 0,
             N'{{"version":1,"data":[]}}', N'History.', N'migration-test', 1, N'test', '{Recorded}', 2, NEWID());

        INSERT INTO IntakeMailClassificationHistory
            (Id, IntakeReceiptId, Version, BeforeJson, AfterJson, Actor, Reason, CorrectedAtUtc)
        VALUES
            (NEWID(), '{HistoryReceipt}', 2,
             N'{{"outcome":0,"category":"billing","isReplyContext":false,"predicates":[],"reason":"Before.","policyKey":"migration-test","policyVersion":1,"caseType":"Inspection"}}',
             N'{{"outcome":0,"category":"cancellation","isReplyContext":false,"predicates":[],"reason":"After.","policyKey":"migration-test","policyVersion":1,"caseType":null}}',
             N'staff:someone', N'Corrected.', '{Recorded}');
        """;
}
