using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20261012180000_FlatMailCategory: a received message has one flat category
/// (operator, 12 October 2026). Each stored family/subtype becomes its
/// category; an Ambiguous, Other or Sent decision becomes Unclassified; the
/// correction history's snapshots take the new shape; the Amendment requests
/// column leaves saved Case list presets; the Ambiguous ownership or
/// destination Unidentified reason becomes No usable identification; and the
/// folder-move and folder-binding tables go.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class FlatMailCategoryMigrationTests
{
    private const string PreviousMigration = "20261012120000_MailboxIncomingOutgoing";
    private const string Recorded = "2031-05-06T10:30:00+00:00";
    private const string HistoryReceipt = "a9000000-0000-0000-0000-000000000001";
    private const string UnidentifiedId = "a9000000-0000-0000-0000-0000000000a1";
    private const string UnidentifiedOrigin = "a9000000-0000-0000-0000-0000000000a2";
    private const string PresetId = "a9000000-0000-0000-0000-0000000000b1";
    private const string SafeDetail = "Two Principals named — £ ownership unclear.";

    // Receipt id suffix, outcome, direction, family, subtype, Other name;
    // then the expected outcome and category.
    private static readonly (string Id, string Outcome, string? Direction, string? Family, string? Subtype, string? OtherName, string ExpectedOutcome, string? ExpectedCategory)[] Decisions =
    [
        ("01", "classified", "received", "post-report-emails", "amendment-request", null, "classified", "query"),
        ("02", "classified", "received", "post-report-emails", "query", null, "classified", "query"),
        ("03", "classified", "received", "in-progress-cases", "chasing-for-update", null, "classified", "case-update"),
        ("04", "classified", "received", "in-progress-cases", "cancellation", null, "classified", "cancellation"),
        ("05", "classified", "received", "pre-instruction-emails", "pre-formal-instruction-request", null, "classified", "pre-instruction"),
        ("06", "classified", "received", "pre-instruction-emails", "triage-request", null, "classified", "triage-request"),
        ("07", "classified", "received", "pre-instruction-emails", "images-received", null, "classified", "images-received"),
        ("08", "classified", "received", "new-instruction-received", "inspection", null, "classified", "new-instruction"),
        ("09", "classified", "received", "General", "autoreply", null, "classified", "general"),
        ("10", "classified", "received", "billing", "remittance", null, "classified", "billing"),
        ("11", "classified", "received", "non-client-related", null, null, "classified", "not-client-related"),
        ("12", "classified", "received", "internal-cc", null, null, "classified", "internal-cc"),
        ("13", "ambiguous", null, null, null, null, "unclassified", null),
        ("14", "classified", "sent", "Report sent", null, null, "unclassified", null),
        ("15", "classified", "received", null, null, "Supplier newsletter", "unclassified", null),
        ("16", "unclassified", null, null, null, null, "unclassified", null),
    ];

    [Fact]
    public async Task MigrationFlattensEachDecisionAndItsHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SeedSql());

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        foreach (var decision in Decisions)
        {
            var actual = await database.ScalarAsync<string>(
                $"SELECT CONCAT(Outcome, N'/', COALESCE(Category, N'-')) FROM IntakeMailClassificationDecisions WHERE IntakeReceiptId = '{ReceiptId(decision.Id)}'");
            Assert.Equal(
                $"{decision.Id}:{decision.ExpectedOutcome}/{decision.ExpectedCategory ?? "-"}",
                $"{decision.Id}:{actual}");
        }
        // An Unclassified decision names no work.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM IntakeMailClassificationDecisions WHERE IntakeReceiptId = '{ReceiptId("08")}' AND CaseType = N'inspection'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM IntakeMailClassificationDecisions WHERE IntakeReceiptId = '{ReceiptId("13")}' AND CaseType IS NULL"));

        foreach (var column in new[] { "Direction", "Family", "Subtype", "OtherName", "OtherReasoning", "AmbiguousCandidatesJson" })
        {
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'IntakeMailClassificationDecisions') AND name = N'{column}'"));
        }
        foreach (var table in new[] { "RetainedMailFolderMoves", "ApprovedMailboxFolderBindings" })
        {
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.tables WHERE name = N'{table}'"));
        }

        // The amendment request became a query; the ambiguous correction became
        // Unclassified. Neither snapshot keeps a retired key.
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM IntakeMailClassificationHistory
            WHERE IntakeReceiptId = '{HistoryReceipt}'
              AND JSON_VALUE(BeforeJson, '$.outcome') = N'0'
              AND JSON_VALUE(BeforeJson, '$.category') = N'query'
              AND JSON_VALUE(BeforeJson, '$.isReplyContext') = N'true'
              AND JSON_VALUE(BeforeJson, '$.policyKey') = N'migration-test'
              AND JSON_VALUE(AfterJson, '$.outcome') = N'1'
              AND JSON_VALUE(AfterJson, '$.category') IS NULL
              AND JSON_VALUE(AfterJson, '$.reason') = N'Corrected to ambiguous.'
            """));
        foreach (var key in new[] { "direction", "family", "subtype", "otherName", "otherReasoning", "ambiguousCandidates" })
        {
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM IntakeMailClassificationHistory WHERE CHARINDEX(N'\"{key}\"', BeforeJson) > 0 OR CHARINDEX(N'\"{key}\"', AfterJson) > 0"));
        }

        Assert.Equal(
            """["case.reference","activity.queries","activity.chases"]""",
            await database.ScalarAsync<string>(
                $"SELECT ColumnKeysJson FROM CaseListPresets WHERE Id = '{PresetId}'"));

        Assert.Equal(
            "NoUsableIdentification",
            await database.ScalarAsync<string>(
                $"SELECT ReasonCode FROM UnidentifiedItems WHERE Id = '{UnidentifiedId}'"));
        // The fingerprint is the one a re-registration of the same origin computes.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '|', "Receipt", Guid.Parse(UnidentifiedOrigin), "NoUsableIdentification", SafeDetail, "SystemWorker", "intake-processing"))));
        Assert.Equal(
            fingerprint,
            await database.ScalarAsync<string>(
                $"SELECT RegistrationFingerprint FROM UnidentifiedItems WHERE Id = '{UnidentifiedId}'"));
    }

    private static string ReceiptId(string suffix) => $"a9000000-0000-0000-0000-0000000001{suffix}";

    private static string Literal(string? value) => value is null ? "NULL" : $"N'{value.Replace("'", "''")}'";

    private static string SeedSql()
    {
        var receipts = new List<string>();
        var decisions = new List<string>();
        foreach (var decision in Decisions)
        {
            receipts.Add(Receipt(ReceiptId(decision.Id), decision.Id));
            var caseType = decision.Id switch { "08" => "N'inspection'", "13" => "N'audit'", _ => "NULL" };
            var otherReasoning = decision.OtherName is null ? "NULL" : "N'No category fits.'";
            decisions.Add(
                $$"""
                ('{{ReceiptId(decision.Id)}}', {{Literal(decision.Outcome)}}, {{Literal(decision.Direction)}}, {{Literal(decision.Family)}},
                 {{Literal(decision.Subtype)}}, {{caseType}}, 0, {{Literal(decision.OtherName)}}, {{otherReasoning}},
                 N'{"version":1,"data":[]}', N'{"version":1,"data":[]}', N'Migration test.', N'migration-test', 1,
                 N'test', '{{Recorded}}', 1, NEWID())
                """);
        }
        receipts.Add(Receipt(HistoryReceipt, "history"));
        decisions.Add(
            $$"""
            ('{{HistoryReceipt}}', N'ambiguous', NULL, NULL, NULL, NULL, 1, NULL, NULL,
             N'{"version":1,"data":["query","case-update"]}', N'{"version":1,"data":[]}', N'Corrected to ambiguous.',
             N'migration-test', 1, N'staff:someone', '{{Recorded}}', 2, NEWID())
            """);

        const string before =
            """{"outcome":0,"direction":0,"family":"post-report-emails","subtype":"amendment-request","isReplyContext":true,"otherName":null,"otherReasoning":null,"ambiguousCandidates":[],"predicates":[{"key":"subject.reply-prefix","matched":true,"detail":"RE:"}],"reason":"Amendment requested.","policyKey":"migration-test","policyVersion":1,"caseType":null,"standaloneAuditReport":null}""";
        const string after =
            """{"outcome":1,"direction":null,"family":null,"subtype":null,"isReplyContext":true,"otherName":null,"otherReasoning":null,"ambiguousCandidates":["query","case-update"],"predicates":[{"key":"subject.reply-prefix","matched":true,"detail":"RE:"}],"reason":"Corrected to ambiguous.","policyKey":"migration-test","policyVersion":1,"caseType":null,"standaloneAuditReport":null}""";
        var oldFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
            '|', "Receipt", Guid.Parse(UnidentifiedOrigin), "AmbiguousOwnershipOrDestination", SafeDetail, "SystemWorker", "intake-processing"))));

        return
            $"""
            INSERT INTO IntakeReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken,
                 ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision,
                 DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson)
            VALUES
            {string.Join(",\n", receipts)};

            INSERT INTO IntakeMailClassificationDecisions
                (IntakeReceiptId, Outcome, Direction, Family, Subtype, CaseType, IsReplyContext, OtherName, OtherReasoning,
                 AmbiguousCandidatesJson, PredicatesJson, Reason, PolicyKey, PolicyVersion, DecidedByActor, DecidedAtUtc,
                 Version, ConcurrencyToken)
            VALUES
            {string.Join(",\n", decisions)};

            INSERT INTO IntakeMailClassificationHistory
                (Id, IntakeReceiptId, Version, BeforeJson, AfterJson, Actor, Reason, CorrectedAtUtc)
            VALUES
                (NEWID(), '{HistoryReceipt}', 2, {Literal(before)}, {Literal(after)}, N'staff:someone',
                 N'Corrected to ambiguous.', '{Recorded}');

            INSERT INTO CaseListPresets
                (Id, Name, ColumnKeysJson, UpdatedBy, UpdatedAtUtc, Version, ConcurrencyToken)
            VALUES
                ('{PresetId}', N'Activity', N'["case.reference","activity.queries","activity.amendment_requests","activity.chases"]',
                 N'staff:someone', '{Recorded}', 1, NEWID());

            INSERT INTO UnidentifiedItems
                (Id, Sequence, Reference, OriginKind, OriginId, ReasonCode, SafeDetail, State, CreatedAtUtc,
                 CreatedByActorKind, CreatedByActorSubjectId, CreatedByActorRolesJson, RegistrationOperationKey,
                 RegistrationFingerprint, Version)
            VALUES
                ('{UnidentifiedId}', 1, N'U-FLAT-1', N'Receipt', '{UnidentifiedOrigin}', N'AmbiguousOwnershipOrDestination',
                 {Literal(SafeDetail)}, N'Open', '{Recorded}', N'SystemWorker', N'intake-processing', N'[]',
                 N'flat-mail-category-migration', N'{oldFingerprint}', 0);
            """;
    }

    private static string Receipt(string id, string label) =>
        $$"""
        ('{{id}}', N'{{label}}.eml', N'message/rfc822', 1, REPLICATE(N'1', 64), N'mailbox',
         N'retained:flat-mail-category-{{label}}', '{{Recorded}}', '{{Recorded}}', N'migration-test', N'1', 0,
         N'case_created', N'Flat category migration.', N'{"version":1,"data":[]}', N'{"version":1,"data":[]}',
         N'{"version":1,"data":[]}')
        """;
}
