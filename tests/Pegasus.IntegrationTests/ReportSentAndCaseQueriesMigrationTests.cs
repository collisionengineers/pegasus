using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261011191701_ReportSentAndCaseQueries: Report sent replaces Completed and
/// a query is a record on the Case (operator, 11 October 2026). Post report,
/// Completed and Query become Report sent, a held Case keeps Report sent as
/// where it returns to, the Completed outcome goes, and a Case that was in
/// Query keeps one open Mail query per post-report e-mail linked to it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ReportSentAndCaseQueriesMigrationTests
{
    private const string PreviousMigration = "20261011112442_DiscardDeletesRepairSpec";
    private const string QueryCaseId = RepairSpecInUseOnCreateMigrationTests.CaseId;
    private const string HeldCaseId = "a8000000-0000-0000-0000-000000000002";
    private const string PostReportCaseId = "a8000000-0000-0000-0000-000000000003";
    private const string CompletedCaseId = "a8000000-0000-0000-0000-000000000004";
    private const string QueryReceiptId = "a8000000-0000-0000-0000-000000000011";
    private const string GeneralReceiptId = "a8000000-0000-0000-0000-000000000012";
    private const string Recorded = "2031-05-06T10:30:00+00:00";
    private const string QueryEntered = "2031-05-08T09:00:00+00:00";

    [Fact]
    public async Task MigrationCollapsesThePostReportStatesAndKeepsEachLinkedQueryOpen()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(RepairSpecInUseOnCreateMigrationTests.CaseWorksBaseline);
        await database.ExecuteAsync(RepairSpecInUseOnCreateMigrationTests.CaseSql);
        await database.ExecuteAsync(CasesSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(WorkflowsSql);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        foreach (var caseId in new[] { QueryCaseId, PostReportCaseId, CompletedCaseId })
        {
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{caseId}' AND State = N'ReportSent' AND PreHoldState IS NULL AND ClosureOutcome IS NULL"));
        }
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{HeldCaseId}' AND State = N'Held' AND PreHoldState = N'ReportSent'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM CaseWorkflows WHERE State IN (N'PostReport', N'PostReportComplete', N'Query') OR PreHoldState IN (N'PostReport', N'PostReportComplete', N'Query') OR ClosureOutcome = N'PostReportComplete'"));

        // The Query Case's linked post-report e-mail holds its one open query,
        // raised by the system when the Case entered Query; its general e-mail
        // raises none.
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseQueries"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseQueries
            WHERE CaseId = '{QueryCaseId}'
              AND IntakeReceiptId = '{QueryReceiptId}'
              AND Source = N'Mail'
              AND Channel IS NULL
              AND Details IS NULL
              AND RaisedByKind = N'SystemWorker'
              AND RaisedAtUtc = '{QueryEntered}'
              AND ClosedAtUtc IS NULL
              AND ClosedAs IS NULL
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{QueryCaseId}' AND HasOpenQuery = 1"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId <> '{QueryCaseId}' AND HasOpenQuery = 1"));

        foreach (var (table, column) in new[]
        {
            ("CaseWorkflows", "HasOpenQuery"),
            ("StaffMailSendOperations", "ResolvesCaseQueries"),
        })
        {
            // A required bit whose default is false.
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"""
                SELECT COUNT(*)
                FROM sys.default_constraints AS defaultConstraint
                INNER JOIN sys.columns AS columnDefinition
                    ON columnDefinition.object_id = defaultConstraint.parent_object_id
                   AND columnDefinition.column_id = defaultConstraint.parent_column_id
                WHERE defaultConstraint.parent_object_id = OBJECT_ID(N'{table}')
                  AND columnDefinition.name = N'{column}'
                  AND columnDefinition.is_nullable = 0
                  AND defaultConstraint.definition IN (N'((0))', N'(CONVERT([bit],(0)))')
                """));
        }
    }

    // Three more Cases on the shared provider, created before CaseWorks so the
    // migration gives each its primary work.
    private const string CasesSql =
        $"""
        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{HeldCaseId}', 'a5000000-0000-0000-0000-000000000013', 'a5000000-0000-0000-0000-000000000012',
             2031, 2, N'RSMG31002', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID()),
            ('{PostReportCaseId}', 'a5000000-0000-0000-0000-000000000013', 'a5000000-0000-0000-0000-000000000012',
             2031, 3, N'RSMG31003', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID()),
            ('{CompletedCaseId}', 'a5000000-0000-0000-0000-000000000013', 'a5000000-0000-0000-0000-000000000012',
             2031, 4, N'RSMG31004', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID());
        """;

    // One Case in each retired post-report state, and the Query Case's two
    // linked mailbox e-mails: a post-report query and a general one.
    private const string WorkflowsSql =
        $$"""
        INSERT INTO CaseWorkflows (CaseId, State, PreHoldState, HeldAtUtc, ClosureOutcome, StateEnteredAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{{QueryCaseId}}', N'Query', NULL, NULL, NULL, '{{QueryEntered}}', 3, NEWID()),
            ('{{HeldCaseId}}', N'Held', N'Query', '{{Recorded}}', NULL, '{{Recorded}}', 3, NEWID()),
            ('{{PostReportCaseId}}', N'PostReport', NULL, NULL, NULL, '{{Recorded}}', 3, NEWID()),
            ('{{CompletedCaseId}}', N'PostReportComplete', NULL, NULL, N'PostReportComplete', '{{Recorded}}', 3, NEWID());

        INSERT INTO IntakeReceipts
            (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken,
             ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision,
             DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson)
        VALUES
            ('{{QueryReceiptId}}', N'query.eml', N'message/rfc822', 1, REPLICATE(N'1', 64), N'mailbox',
             N'retained:report-sent-migration-query', '{{Recorded}}', '{{Recorded}}', N'migration-test', N'1', 0,
             N'case_created', N'Post-report query.', N'{"version":1,"data":[]}', N'{"version":1,"data":[]}',
             N'{"version":1,"data":[]}'),
            ('{{GeneralReceiptId}}', N'general.eml', N'message/rfc822', 1, REPLICATE(N'2', 64), N'mailbox',
             N'retained:report-sent-migration-general', '{{Recorded}}', '{{Recorded}}', N'migration-test', N'1', 0,
             N'case_created', N'General correspondence.', N'{"version":1,"data":[]}', N'{"version":1,"data":[]}',
             N'{"version":1,"data":[]}');

        INSERT INTO IntakeMailClassificationDecisions
            (IntakeReceiptId, Outcome, Direction, Family, Subtype, IsReplyContext, AmbiguousCandidatesJson,
             PredicatesJson, Reason, PolicyKey, PolicyVersion, DecidedByActor, DecidedAtUtc, Version,
             ConcurrencyToken)
        VALUES
            ('{{QueryReceiptId}}', N'classified', N'received', N'post-report-emails', N'query', 0,
             N'{"version":1,"data":[]}', N'{"version":1,"data":[]}', N'Migration test.', N'migration-test', 1,
             N'test', '{{Recorded}}', 1, NEWID()),
            ('{{GeneralReceiptId}}', N'classified', N'received', N'General', N'case-summary', 0,
             N'{"version":1,"data":[]}', N'{"version":1,"data":[]}', N'Migration test.', N'migration-test', 1,
             N'test', '{{Recorded}}', 1, NEWID());

        INSERT INTO IntakeManualAssociations
            (IntakeReceiptId, CaseId, IsActive, Version, LinkedAtUtc, ActorKind, ActorSubjectId, ActorRolesJson,
             Reason, LastOperationKey)
        VALUES
            ('{{QueryReceiptId}}', '{{QueryCaseId}}', 1, 0, '{{Recorded}}', N'Staff', N'staff', N'[]',
             N'Linked query.', N'link-query'),
            ('{{GeneralReceiptId}}', '{{QueryCaseId}}', 1, 0, '{{Recorded}}', N'Staff', N'staff', N'[]',
             N'Linked general.', N'link-general');
        """;
}
