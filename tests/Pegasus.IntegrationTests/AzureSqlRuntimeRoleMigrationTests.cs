using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.IntegrationTests.Support;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The latest schema, restored once from the per-run template for the
/// read-only tests of <see cref="AzureSqlRuntimeRoleMigrationTests"/>.
/// </summary>
public sealed class RuntimeRoleSchemaFixture : IAsyncLifetime
{
    internal LocalDbTestDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync() => Database = await LocalDbTestDatabase.CreateAsync();

    public async Task DisposeAsync()
    {
        if (Database is not null)
        {
            await Database.DisposeAsync();
        }
    }
}

[Trait("Category", "SqlServer")]
public sealed class AzureSqlRuntimeRoleMigrationTests(RuntimeRoleSchemaFixture fixture)
    : IClassFixture<RuntimeRoleSchemaFixture>
{
    /// <summary>
    /// The Worker links a Triage that already holds a finding and fills the
    /// linked Case's empty findings from it (operator, 7 October 2026), under
    /// its own runtime role: the link and the fill each advance the Case once.
    /// </summary>
    [Fact]
    public async Task WorkerRuntimeAutomaticallyLinksTriageFillsTheCaseAndReplays()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEngineerTriageRequest("triage-worker-recovery.eml");
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, email);
        await using (var findingScope = factory.Services.CreateAsyncScope())
        {
            var services = findingScope.ServiceProvider;
            var staff = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
            var triageId = (await services.GetRequiredService<ITriageQueries>().ListAsync(null, CancellationToken.None))
                .Single().CaseId;
            var editLease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
                new(EditScopeKind.Triage, triageId, 0, staff, "worker-fill-finding-edit"),
                CancellationToken.None);
            await services.GetRequiredService<IRecordTriageFinding>().ExecuteAsync(
                new RecordTriageFindingRequest(
                    triageId, 0, staff, "worker-fill-finding", "Seen in the images",
                    RoadworthinessFinding.Roadworthy, AssessmentFinding.Repairable, null)
                {
                    EditLeaseToken = editLease.Token
                },
                CancellationToken.None);
        }
        var caseId = await QdosTriageIntegrationTests.SeedMatchingFormalCaseAsync(factory.Services, receiptId);
        await (await factory.DatabaseAsync).ExecuteAsync($"""
            CREATE USER [pegasus_test_triage_pairing_worker] WITHOUT LOGIN;
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_test_triage_pairing_worker];
            """);
        await using var connection = factory.Database.CreateConnection();
        await connection.OpenAsync();
        await using var impersonation = connection.CreateCommand();
        impersonation.CommandText = "EXECUTE AS USER = N'pegasus_test_triage_pairing_worker';";
        await impersonation.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(connection).Options;
            var store = new EfTriageStore(new ConnectedContextFactory(options),
                [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())]);
            var pairing = new TriageCasePairing(store);
            Assert.Equal(new TriageCasePairingResult(1, 1, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
            Assert.Equal(new TriageCasePairingResult(0, 0, 0), await pairing.ReconcileAsync(1, CancellationToken.None));
        }
        finally
        {
            impersonation.CommandText = "REVERT;";
            await impersonation.ExecuteNonQueryAsync();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var triage = await context.Triage.AsNoTracking().SingleAsync(item => item.OriginReceiptId == receiptId);
        Assert.Equal(caseId, triage.LinkedInstructionCaseId);
        // The link closes the Triage: Completed, because a finding was recorded
        // (operator, 26 October 2026), and the closure is the extra version.
        Assert.Equal("completed", triage.State);
        Assert.Equal(3, triage.Version);
        var triageCase = await context.Cases.AsNoTracking().SingleAsync(item => item.Id == triage.CaseId);
        Assert.StartsWith("t.", triageCase.Reference, StringComparison.Ordinal);
        var instructionCase = await context.Cases.AsNoTracking().SingleAsync(item => item.Id == caseId);
        Assert.Equal(
            $"Completed: linked to {instructionCase.Reference}.",
            (await context.TriageHistory.AsNoTracking().SingleAsync(item =>
                item.TriageCaseId == triage.CaseId && item.EventType == "triage_state_completed")).Reason);
        var history = await context.TriageHistory.AsNoTracking().SingleAsync(item =>
            item.TriageCaseId == triage.CaseId && item.EventType == "triage_case_linked");
        Assert.Equal(nameof(ActorKind.SystemWorker), history.ActorKind);
        Assert.Equal(TriageCasePairing.ActorId, history.Actor);
        Assert.Equal(1, await context.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == caseId && item.EventType == history.EventType));
        // The instructed Case and the Triage Case, which is a Case too.
        Assert.Equal(2, await context.Cases.CountAsync());
        Assert.Equal(2, (await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId)).Version);
        var filled = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == caseId)
            .OrderBy(item => item.FieldPath)
            .ToListAsync();
        Assert.Equal(
            [
                (AssessmentVocabulary.LegalStatus, "roadworthy"),
                (AssessmentVocabulary.Outcome, "repairable")
            ],
            filled.Select(item => (item.FieldPath, item.Value)).ToArray());
        Assert.All(filled, item =>
        {
            Assert.Equal(nameof(ActorKind.Automation), item.RecordedByKind);
            Assert.Equal(TriageFindingFill.RecorderId, item.RecordedBy);
        });
        Assert.Equal(1, await context.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == caseId && item.EventType == "triage_finding_filled"));
    }

    private const string V1FoundationMigration = "20260906054658_V1PlatformFoundation";
    private const string FilterActiveReportGenerationMigration =
        "20260906170000_FilterActiveCaseReportGenerationSnapshot";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    private const string FoundationTableSpec = """
        AppliedValuationSnapshots
        CaseReportGenerations
        ContactPrincipalLinks
        DocumentContentCacheEntries
        EditScopes
        GeneratedCaseArtifacts
        GlassRepairEstimateSessions
        IntakeOcrOperations
        IntakeSourceCandidates
        LabourRateCards
        RetainedInstructionAnalyses
        StaffMailSendOperations
        UserExternalCredentials
        ValuationPresets
        """;

    [Fact]
    public async Task LatestSchemaKeepsBootstrapRemovedAndTheOpenIddictTables()
    {
        var database = fixture.Database;

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'RequestUploadLinks', N'RequestUploadReceipts', N'PublicUploadSessions', N'PublicUploadOccurrences')"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CaseDueChasers') AND name IN (N'RequestLinkReference', N'RequestLinkPurpose')"));

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ApplicationInitializations'"));
        // 20260803151159_AutomationActorOpenIddict re-creates the four
        // OpenIddict tables for the Automation Actor client-credentials
        // ingress with the Web-only least-privilege posture they previously
        // held: OpenIddict state stays owned by the Web process, scopes are
        // read-only, and DELETE is denied to both runtime roles.
        Assert.Equal(4, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.tables
            WHERE name IN (
                N'OpenIddictApplications',
                N'OpenIddictAuthorizations',
                N'OpenIddictScopes',
                N'OpenIddictTokens')
            """));
        // The third-party vehicle flag is gone: image tags carry the
        // classification, and the case export exclusion reads the Third party tag.
        Assert.Equal(
            0,
            await database.ScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[dbo].[DocumentOccurrences]')
                  AND name IN (
                      N'ThirdPartyVehicleConfirmationOperationKey',
                      N'ThirdPartyVehicleConfirmationReason',
                      N'ThirdPartyVehicleConfirmedAtUtc')
                """));
        Assert.Equal(
            0,
            await database.ScalarAsync<int>(
                """
                SELECT COUNT(*)
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'[dbo].[DocumentOccurrences]')
                  AND name = N'IX_DocumentOccurrences_CaseId_ThirdPartyVehicleConfirmedAtUtc'
                """));
    }

    /// <summary>
    /// The image-tag tables and the exact permissions each runtime caller
    /// needs: Web reads the vocabulary, adds to it and moves tags on and off;
    /// the Worker only reads, because the case export asks which images wear
    /// Third party. Neither may update either table, and only Web may delete a
    /// tag from an image.
    /// </summary>
    [Fact]
    public async Task LatestSchemaSeedsTheBuiltInImageTags()
    {
        var database = fixture.Database;

        Assert.Equal(
            ["Close-up", "Overview", "Reflection", "Third party"],
            await ReadValuesAsync(
                database,
                """
                SELECT Name COLLATE DATABASE_DEFAULT
                FROM [dbo].[ImageTags]
                WHERE IsBuiltIn = 1
                """));
    }

    [Fact]
    public async Task LatestSchemaDropsDeadBoxRequests()
    {
        var database = fixture.Database;

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'BoxFileRequests'"));
    }

    [Fact]
    public async Task LatestSchemaKeepsTheFoundationTablesAndTheirConstraints()
    {
        var database = fixture.Database;

        var tables = ParseLines(FoundationTableSpec);
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM [dbo].[ApprovedMailboxes] WHERE [State] = N'Approved' AND [MailboxGeneration] <> 1"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE [name] = N'CK_ApprovedMailboxes_MailboxGeneration' AND [is_disabled] = 0 AND [is_not_trusted] = 0"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE [object_id] = OBJECT_ID(N'[dbo].[DocumentVersions]') AND [name] = N'PendingContentStorageKey' AND [max_length] = 400 AND [is_nullable] = 1"));
        Assert.Equal(
            tables,
            await ReadValuesAsync(
                database,
                $"""
                SELECT name
                FROM sys.tables
                WHERE name IN ({string.Join(", ", tables.Select(table => $"N'{table}'"))})
                """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM (
                SELECT indexDefinition.object_id, indexDefinition.index_id
                FROM sys.indexes AS indexDefinition
                INNER JOIN sys.tables AS tableDefinition
                    ON tableDefinition.object_id = indexDefinition.object_id
                INNER JOIN sys.index_columns AS indexColumn
                    ON indexColumn.object_id = indexDefinition.object_id
                   AND indexColumn.index_id = indexDefinition.index_id
                   AND indexColumn.key_ordinal > 0
                INNER JOIN sys.columns AS columnDefinition
                    ON columnDefinition.object_id = indexColumn.object_id
                   AND columnDefinition.column_id = indexColumn.column_id
                WHERE tableDefinition.name IN ({string.Join(", ", tables.Select(table => $"N'{table}'"))})
                GROUP BY indexDefinition.object_id, indexDefinition.index_id, indexDefinition.[type]
                HAVING SUM(columnDefinition.max_length) >
                    CASE WHEN indexDefinition.[type] = 1 THEN 900 ELSE 1700 END
            ) AS oversizedIndex
            """));
    }

    // 20261011191701_ReportSentAndCaseQueries: Web logs, tags and resolves
    // queries; the Worker raises one when a post-report e-mail links
    // automatically and resolves them when a flagged reply reaches Sent.
    // Neither deletes one.
    // 20261017110000_InspectionLocations: the Case page reads the reviewed
    // locations and their links, and a Principal's replacement in the Web
    // copies its links to the successor. The register loader writes as the
    // operator, outside both runtime roles, and neither role deletes a row.
    // 20261023160000_AppliedValuationCalculationRows: the Case save and Create
    // audit's copy write an applied valuation's calculation rows and the Case
    // page reads them, as they do the snapshot table: SELECT and INSERT for Web,
    // no DELETE for either role, and the Worker has no caller.
    // 20260924180000_CaseWorksAndTriageCases: Web and Worker both create Cases
    // (and so their primary work); Web updates a work (Create audit moves the
    // Inspection report's evidence onto it), and since
    // 20261012120000_WorkerObservesReportSends so does the Worker (the Sent
    // poll links the Inspection report's evidence after the Audit exists);
    // neither deletes one. Web also creates Triage Cases.
    // 20260927002303_EmailTemplates: Web alone reads and writes the saved
    // e-mail templates and neither role deletes one; the Worker has no part in
    // them.
    // 20261002105641_WorkCentreDismissals: Web records each Work Centre
    // dismissal and moves a later one on; there is no undo, so neither role
    // deletes one, and the Worker has no part in it.
    // 20261012192000_GrantWorkerCaseTasksForReportSent: the Worker's Sent-evidence link
    // creates the after-send tasks the send recorded, in the same transaction as Report
    // sent. It inserts tasks, never updating or deleting one; Web's task grants are
    // unchanged. The tests run full-privilege, so only this assertion catches a missing
    // grant before production refuses the write.
    // 20261018080000_GrantWorkerContactPrincipalLinksRead: the Worker's mail route
    // evaluation reads which Principal each Claim Source is linked to, to recognise an
    // intermediary. It reads the links only; Web's link grants are unchanged. The tests
    // run full-privilege, so only this assertion catches a missing grant.
    // A complete vehicle lookup answer that no longer carries engine, fuel,
    // colour, tax expiry or MOT expiry clears that fact, so the Worker deletes
    // the CaseAssessmentFields row it recorded.
    // 20260928090000_GrantWorkerGeneratedCaseArtifactUpdate: the Worker's sweep
    // records a generated report file as stored once custody has filed it, so
    // it updates the artifact row and its generation. It never inserts or
    // deletes either.
    // Case-document registration moved into the Worker's
    // custody processor while these three tables were granted to Web only, so
    // every deployed case uploaded its evidence to Box and was then refused the
    // record write. Nothing here caught it because the tests run
    // full-privilege; this asserts the grant itself.
    // 20260928160000_GrantWorkerDocumentOccurrenceUpdate: the Worker gives the
    // original report it recognised among a receipt's filed files the Audit
    // report role, so it updates an occurrence too.
    // 20261005120000_GrantWorkerCaseManualChases: a Case chaser that reaches
    // Sent is recorded as a chase by the Worker's Sent poll, which held no
    // permission on the chase table. Nothing else caught it because the tests
    // run full-privilege; this asserts the grant itself.
    // 20261007183000_GrantWorkerTriageFindings: the automatic Triage link runs
    // as the Worker and now reads the Triage's current finding to fill the
    // linked Case's empty findings.
    // 20261012120000_WorkerObservesReportSends: the Sent poll observes a report
    // send as the Worker and freezes its Sent repair specification. The
    // CaseWorks UPDATE it also gains is pinned with that table's exact set.
    // 20261021120000_GrantWebRepairSpecDiscardDelete: Discard in the Web
    // process deletes the spec, its lines and its numbered versions (FRD-25;
    // operator, 11 October 2026). Web holds DELETE on all three tables; the
    // Worker never discards and keeps its snapshot DELETE denial.
    // 20260929091000_GrantWebRetainedMailDismissal: Inbox Dismiss and Restore
    // write the dismissal cells of the retained message row as the Web role,
    // which held SELECT only. The grant is table-level; the exact list below
    // proves no DELETE grant.
    // 20261017100000_VehicleLookupMileageEstimate: a Case save that replaces the
    // Case mileage the MOT mileage estimate filled records its disposition on the
    // lookup observation as the Web role, which held SELECT only. The grant is
    // table-level; the exact lists below prove no DELETE grant and the Worker's
    // grants unchanged.
    // 20260929150000_GrantCaseDataSnapshotUpdate: completing a Case's images
    // (Worker) and a Case save (Web) record re-evaluated readiness on the
    // snapshot row, which both roles could only read and insert. Production
    // refused every such write while these tests, running full-privilege,
    // passed; this asserts the grant itself.
    // The same migration queues again a fold the refused snapshot write failed:
    // its files are already in the Case folder and nothing was recorded. A
    // fold that failed for any other reason stays failed.
    [Fact]
    public async Task GrantCaseDataSnapshotUpdateQueuesAgainOnlyTheFoldsTheRefusalFailed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync("20260929120000_PrincipalVocabulary");
        var refused = Guid.NewGuid();
        var otherFailure = Guid.NewGuid();
        await database.ExecuteAsync(
            $"""
            INSERT INTO [dbo].[ExternalWorkItems]
                ([Id], [Kind], [OperationKey], [State], [AttemptCount], [DueAtUtc],
                 [LeaseToken], [LeaseExpiresAtUtc], [FailureCode], [FailureReason], [CompletedAtUtc])
            VALUES
                ('{refused}', N'merge_image_case_custody', N'fold-refused', N'failed', 2,
                 '2026-09-29T10:16:15+00:00', N'lease', '2026-09-29T10:21:15+00:00',
                 N'custody_unexpected_failure:DbUpdateException', N'Case evidence could not be stored.',
                 '2026-09-29T10:16:16+00:00'),
                ('{otherFailure}', N'merge_image_case_custody', N'fold-other', N'failed', 1,
                 '2026-09-29T10:16:15+00:00', NULL, NULL,
                 N'source_integrity_conflict', N'The retained evidence no longer matches the accepted source.',
                 '2026-09-29T10:16:16+00:00');
            """);

        await context.Database.MigrateAsync();

        Assert.Equal(
            ["pending||||"],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT([State], N'|', [LeaseToken], N'|', [FailureCode], N'|', [FailureReason], N'|', [CompletedAtUtc])
                FROM [dbo].[ExternalWorkItems] WHERE [Id] = '{refused}'
                """));
        Assert.Equal(
            ["failed|source_integrity_conflict"],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT([State], N'|', [FailureCode])
                FROM [dbo].[ExternalWorkItems] WHERE [Id] = '{otherFailure}'
                """));
    }

    // The Worker's thumbnail sweep lists confirmed versions and reads and
    // writes the content cache as the least-privilege Worker role. No grant
    // was added for it; this asserts the ones it relies on. The cache publisher
    // the Worker runs at filing (IDocumentContentCachePublisher) inserts and
    // updates the same table, so it relies on the same grants and needs no
    // migration of its own.
    // Catalog-grant checks alone run as the LocalDB administrator and
    // therefore cannot detect a real runtime save denied by SQL Server. These
    // loginless users have only their corresponding Pegasus runtime role.
    [Fact]
    public async Task WorkerRuntimeClaimsTheIntakeHandOverThroughTheCustodyStore()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var receiptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        await database.ExecuteAsync(
            $"""
            INSERT INTO [dbo].[IntakeReceipts] (
                [Id], [SourceFileName], [MediaType], [SourceLength], [SourceHash],
                [SourceChannel], [ExternalReceiptToken], [ReceivedAtUtc], [ProcessedAtUtc],
                [SourceReaderKey], [SourceReaderVersion], [Version], [Decision],
                [DecisionReason], [EvidenceJson], [FieldsJson], [OcrCandidatesJson])
            VALUES (
                '{receiptId:D}', N'ocr-permission.eml', N'message/rfc822', 1,
                REPLICATE(N'A', 64), N'manual_upload', N'ocr-permission:{receiptId:N}',
                '2031-05-06T10:30:00+00:00', '2031-05-06T10:31:00+00:00',
                N'runtime-role-test', N'1', 0, N'retained',
                N'runtime role permission fixture', N'[]', N'{"{}"}', N'[]');
            INSERT INTO [dbo].[IntakeAssets] (
                [Id], [IntakeReceiptId], [SourceLabel], [FileName], [MediaType],
                [Kind], [Disposition], [ContentLength], [ContentHash], [StorageKey])
            VALUES (
                '{assetId:D}', '{receiptId:D}', N'attachment', N'evidence.pdf',
                N'application/pdf', N'attachment', N'retained', 1,
                REPLICATE(N'B', 64), N'runtime-role/{assetId:N}');
            """);

        // Exercise the actual EF custody claim as Worker, not merely the
        // permission census: intake retention uses only the intake asset row.
        await database.ExecuteAsync($"""
            CREATE USER [pegasus_test_custody_worker] WITHOUT LOGIN;
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_test_custody_worker];
            """);
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var impersonation = connection.CreateCommand();
        impersonation.CommandText = "EXECUTE AS USER = N'pegasus_test_custody_worker';";
        await impersonation.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(connection).Options;
            var custody = new EfIncomingArtifactRetentionStore(new ConnectedContextFactory(options));
            Assert.False(await custody.TryClaimHandOverAsync($"intake:{Guid.NewGuid():N}:{assetId:N}", CancellationToken.None));
            Assert.True(await custody.TryClaimHandOverAsync($"intake:{receiptId:N}:{assetId:N}", CancellationToken.None));
            Assert.False(await custody.TryClaimHandOverAsync($"intake:{receiptId:N}:{assetId:N}", CancellationToken.None));
        }
        finally
        {
            impersonation.CommandText = "REVERT;";
            await impersonation.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// The Worker reads a standalone Audit's retained original report inside
    /// automatic acceptance (<see cref="ReadOriginalReport.ForIntakeAsync"/>).
    /// Every SQL that read issues must succeed under the Worker's own role:
    /// a denied table is swallowed as "the report could not be read" and the
    /// Audit is created with its Original report cells blank, which is how
    /// Release 79's contact lookup (a join to a table the Worker may not
    /// read) emptied every Exclusive Audit's cells in production. The real
    /// stores run here; only the bytes and the PDF reader are stand-ins.
    /// </summary>
    [Fact]
    public async Task WorkerRuntimeReadsAStandaloneAuditsOriginalReportAtAcceptance()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var receiptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var reportBytes = Encoding.UTF8.GetBytes("%PDF synthetic original report");
        // Mail intake records the asset hash in capitals.
        var reportHash = Convert.ToHexString(SHA256.HashData(reportBytes));
        var emptyEnvelope = EfIntakeReceiptStore.SerializeEnvelope(Array.Empty<string>());
        await database.ExecuteAsync($"""
            INSERT INTO [dbo].[IntakeReceipts] (
                [Id], [SourceFileName], [MediaType], [SourceLength], [SourceHash],
                [SourceChannel], [ExternalReceiptToken], [ReceivedAtUtc], [ProcessedAtUtc],
                [SourceReaderKey], [SourceReaderVersion], [Version], [Decision],
                [DecisionReason], [EvidenceJson], [FieldsJson], [OcrCandidatesJson])
            VALUES
                ('{receiptId:D}', N'audit.eml', N'message/rfc822', 1,
                 REPLICATE(N'A', 64), N'mailbox', N'original-report-role:{receiptId:N}',
                 '2031-05-06T10:30:00+00:00', '2031-05-06T10:31:00+00:00',
                 N'runtime-role-test', N'1', 1, N'case_created', N'Standalone Audit fixture',
                 N'{emptyEnvelope}', N'{emptyEnvelope}', N'{emptyEnvelope}');
            INSERT INTO [dbo].[IntakeAssets] (
                [Id], [IntakeReceiptId], [SourceLabel], [FileName], [MediaType],
                [Kind], [Disposition], [ContentLength], [ContentHash], [StorageKey])
            VALUES (
                '{assetId:D}', '{receiptId:D}', N'uploaded audit.eml, attachment 2: report.pdf', N'report.pdf',
                N'application/pdf', N'attachment', N'attachment', {reportBytes.Length}, N'{reportHash}',
                N'runtime-role/{assetId:N}');
            INSERT INTO [dbo].[StandaloneAuditEvidence] (
                [Id], [IntakeReceiptId], [OriginalReportAssetId], [Assessment], [ConfirmedByKind],
                [ConfirmedBySubjectId], [ConfirmedByRolesJson], [ConfirmedAtUtc], [OperationKey],
                [Reason], [RequestHash], [ResultingReceiptVersion])
            VALUES (
                '{evidenceId:D}', '{receiptId:D}', '{assetId:D}', N'repairable', N'SystemWorker',
                N'system-worker:automatic-standalone-audit', N'[]', '2031-05-06T10:31:00+00:00',
                N'automatic-standalone-audit:{receiptId:N}', N'The retained original report states Repairable.',
                REPLICATE(N'b', 64), 1);
            CREATE USER [pegasus_test_original_report_worker] WITHOUT LOGIN;
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_test_original_report_worker];
            """);

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var impersonation = connection.CreateCommand();
        impersonation.CommandText = "EXECUTE AS USER = N'pegasus_test_original_report_worker';";
        await impersonation.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(connection).Options;
            var contextFactory = new ConnectedContextFactory(options);
            var artifacts = new FixedArtifactStore($"runtime-role/{assetId:N}", reportBytes);
            var reader = new ReadOriginalReport(
                new ConnexusReportReader(),
                new EfStandaloneAuditEvidenceStore(contextFactory, artifacts, TimeProvider.System),
                new EfIntakeReceiptStore(contextFactory),
                artifacts,
                new NoCaseDocumentMetadata(),
                new NoLogicalDocumentVersionReader(),
                new EfIntakeOcrOperationStore(contextFactory),
                TimeProvider.System);

            var reading = await reader.ForIntakeAsync(receiptId, evidenceId, CancellationToken.None);

            Assert.NotNull(reading);
            Assert.Equal(reportHash.ToLowerInvariant(), reading.Sha256.ToLowerInvariant());
            Assert.Equal("Connexus Vehicle Assessors", reading.Assessor);
            Assert.Equal("2026-03-09", reading.ReportDate);
            Assert.Equal("unroadworthy", reading.Roadworthiness);
            Assert.Equal("repairable", reading.Outcome);
        }
        finally
        {
            impersonation.CommandText = "REVERT;";
            await impersonation.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task WorkerReconcilesRegisteredImageUsingCurrentCaseIdentityExactlyOnce()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var context = await database.CreateContextAsync();

        var caseReceiptId = Guid.NewGuid();
        var imageReceiptId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var seededPrincipal = await SeededPrincipals.QdosAsync(context);
        var principalId = seededPrincipal.Id;
        var lineageId = seededPrincipal.SequenceLineageId;
        var emptyEnvelope = EfIntakeReceiptStore.SerializeEnvelope(Array.Empty<string>());

        // Owner-side persisted-state fixture, not a newly received instruction.
        // The supplied QDOS registrations deliberately differ: the current index,
        // not the original draft, identifies this already-registered image.
        await database.ExecuteAsync($"""
            INSERT INTO [dbo].[IntakeReceipts] (
                [Id], [SourceFileName], [MediaType], [SourceLength], [SourceHash],
                [SourceChannel], [ExternalReceiptToken], [ReceivedAtUtc], [ProcessedAtUtc],
                [SourceReaderKey], [SourceReaderVersion], [Version], [Decision],
                [DecisionReason], [EvidenceJson], [FieldsJson], [OcrCandidatesJson])
            VALUES
                ('{caseReceiptId:D}', N'instruction.eml', N'message/rfc822', 1,
                 REPLICATE(N'A', 64), N'manual_upload', N'image-role-case:{caseReceiptId:N}',
                 '2031-05-06T10:30:00+00:00', '2031-05-06T10:31:00+00:00',
                 N'runtime-role-test', N'1', 0, N'case_created', N'Existing Case fixture',
                 N'{emptyEnvelope}', N'{emptyEnvelope}', N'{emptyEnvelope}'),
                ('{imageReceiptId:D}', N'1_CLVoffside-V1.jpg', N'image/jpeg', 1,
                 REPLICATE(N'B', 64), N'mailbox', N'image-role-source:{imageReceiptId:N}',
                 '2031-05-06T10:29:00+00:00', '2031-05-06T10:29:00+00:00',
                 N'runtime-role-test', N'1', 0, N'image_intake_registered', N'Registered image fixture',
                 N'{emptyEnvelope}', N'{emptyEnvelope}', N'{emptyEnvelope}');
            INSERT INTO [dbo].[IntakeAssets] (
                [Id], [IntakeReceiptId], [SourceLabel], [FileName], [MediaType],
                [Kind], [Disposition], [ContentLength], [ContentHash], [StorageKey])
            VALUES (
                '{Guid.NewGuid():D}', '{imageReceiptId:D}', N'source', N'1_CLVoffside-V1.jpg',
                N'image/jpeg', N'source', N'source', 1, REPLICATE(N'B', 64),
                N'runtime-role/{imageReceiptId:N}');
            INSERT INTO [dbo].[InstructionDrafts] ([IntakeReceiptId], [VehicleRegistration])
            VALUES ('{caseReceiptId:D}', N'NG22FVH');
            INSERT INTO [dbo].[Cases] (
                [Id], [PrincipalId], [SequenceLineageId], [Year], [Sequence], [Reference],
                [Type], [InitialState], [CustodyState], [OriginIntakeReceiptId],
                [InstructionComplete], [ImagesComplete], [CreatedAtUtc], [Version], [ConcurrencyToken])
            VALUES ('{caseId:D}', '{principalId:D}', '{lineageId:D}', 2031, 1, N'QDOS31001',
                N'inspection', N'review', N'pending', '{caseReceiptId:D}', 1, 1,
                '2031-05-06T10:30:00+00:00', 0, '{Guid.NewGuid():D}');
            INSERT INTO [dbo].[CaseWorks] ([Id], [CaseId], [Kind], [CreatedAtUtc])
            VALUES ('{caseId:D}', '{caseId:D}', N'primary', '2031-05-06T10:30:00+00:00');
            INSERT INTO [dbo].[CaseWorkflows] ([CaseId], [State], [Version], [ConcurrencyToken])
            VALUES ('{caseId:D}', N'{nameof(CaseLifecycleState.Review)}', 0, '{Guid.NewGuid():D}');
            INSERT INTO [dbo].[CaseMatchIndex] (
                [CaseId], [PrincipalCode], [NormalizedVrm], [MatchPolicyKey],
                [MatchPolicyVersion], [UpdatedAtUtc])
            VALUES ('{caseId:D}', N'QDOS', N'PG18BTY', N'runtime-role-test', 1,
                '2031-05-06T10:31:00+00:00');
            INSERT INTO [dbo].[ImageIntakes] (
                [Id], [OriginReceiptId], [SourceChannel], [ExternalReceiptToken], [SourceHash],
                [EvaluationRevisionId], [NormalizedVehicleRegistration], [ImageIntakeReference],
                [PrincipalId], [CreatedAtUtc], [CreatedByActorKind], [CreatedByActorSubjectId],
                [Reason], [CreationOperationKey], [RequestFingerprint], [LifecycleState],
                [LifecycleVersion], [CustodyState])
            VALUES ('{imageId:D}', '{imageReceiptId:D}', N'mailbox',
                N'image-role-source:{imageReceiptId:N}', REPLICATE(N'B', 64), '{Guid.NewGuid():D}',
                N'PG18BTY', N'PG18BTY-01', '{principalId:D}', '2031-05-06T10:29:00+00:00',
                N'SystemWorker', N'image-intake-automation', N'Registered image fixture',
                N'image-role-register:{imageReceiptId:N}', REPLICATE(N'C', 64),
                N'awaiting_instruction', 0, N'pending');
            CREATE USER [pegasus_test_image_recovery_worker] WITHOUT LOGIN;
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_test_image_recovery_worker];
            """);

        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var impersonation = connection.CreateCommand();
        impersonation.CommandText = "EXECUTE AS USER = N'pegasus_test_image_recovery_worker';";
        await impersonation.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(connection).Options;
            var workerFactory = new ConnectedContextFactory(options);
            var pairing = new ImageIntakeCasePairing(
                new EfImageIntakeStore(workerFactory),
                new EfImageIntakeCaseCandidates(workerFactory),
                new EfIntakeMutationStore(workerFactory),
                TimeProvider.System,
                new DiscardingCommittedWorkPublisher(),
                new EfIntakeReceiptStore(workerFactory));

            Assert.Equal(new ImageIntakePairingResult(1, 1, 0),
                await pairing.ReconcileAsync(1, CancellationToken.None));
            Assert.Equal(new ImageIntakePairingResult(0, 0, 0),
                await pairing.ReconcileAsync(1, CancellationToken.None));
        }
        finally
        {
            impersonation.CommandText = "REVERT;";
            await impersonation.ExecuteNonQueryAsync();
        }

        var association = await context.IntakeManualAssociations.AsNoTracking()
            .SingleAsync(item => item.IntakeReceiptId == imageReceiptId);
        Assert.True(association.IsActive);
        Assert.Equal(caseId, association.CaseId);
        Assert.Equal(nameof(ActorKind.SystemWorker), association.ActorKind);
        Assert.Equal(ImageIntakeAutomation.ActorId, association.ActorSubjectId);
        var image = await context.ImageIntakes.AsNoTracking().SingleAsync(item => item.Id == imageId);
        Assert.Equal("merged_into_instruction_case", image.LifecycleState);
        Assert.Equal(1, image.LifecycleVersion);
        Assert.Equal(caseId, image.MergedIntoCaseId);
        Assert.Equal("QDOS31001", image.MergedIntoCaseReference);
        var lifecycle = await context.ImageIntakeLifecycleEvents.AsNoTracking()
            .SingleAsync(item => item.ImageIntakeId == imageId);
        Assert.Equal("merged_into_instruction_case", lifecycle.EventType);
        Assert.Equal(nameof(ActorKind.SystemWorker), lifecycle.ActorKind);
        Assert.Equal(ImageIntakeAutomation.ActorId, lifecycle.ActorSubjectId);
        Assert.Equal(caseId, lifecycle.CaseId);
        var custody = await context.ExternalWorkItems.AsNoTracking()
            .SingleAsync(item => item.ImageIntakeId == imageId);
        Assert.Equal(ExternalWorkKinds.MergeImageCaseCustody, custody.Kind);
        Assert.Equal(caseId, custody.CaseId);
        Assert.Equal("pending", custody.State);
        Assert.Equal(1, await context.IntakeMutationHistory.CountAsync(item =>
            item.IntakeReceiptId == imageReceiptId && item.EventType == "intake_case_auto_linked"));
        Assert.Equal(1, await context.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == caseId && item.EventType == "intake_case_auto_linked"));
        Assert.Equal(1, await context.CaseHistory.CountAsync(item =>
            item.CaseId == caseId && item.EventType == "image_initiated_case_merged"));
        Assert.Equal(1, await context.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == caseId && item.EventType == "image_initiated_case_merged"));
        Assert.Equal("NG22FVH", await context.InstructionDrafts.Where(item =>
            item.IntakeReceiptId == caseReceiptId).Select(item => item.VehicleRegistration).SingleAsync());
        Assert.Equal("PG18BTY", await context.CaseMatchIndex.Where(item =>
            item.CaseId == caseId).Select(item => item.NormalizedVrm).SingleAsync());
    }

    [Fact]
    public async Task LatestSchemaKeepsReplyToAddressesJsonNullable()
    {
        var database = fixture.Database;

        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns AS column_value
            INNER JOIN sys.types AS type_value
                ON type_value.user_type_id = column_value.user_type_id
            WHERE column_value.object_id = OBJECT_ID(N'[dbo].[RetainedMailboxMessages]')
              AND column_value.name = N'ReplyToAddressesJson'
              AND type_value.name = N'nvarchar'
              AND column_value.max_length = -1
              AND column_value.is_nullable = 1
            """));
    }

    [Fact]
    public async Task LatestSchemaRetainsOneLabourRateAndRemovesTheObsoletePaintRate()
    {
        var database = fixture.Database;
        await using var context = await database.CreateContextAsync();

        Assert.Equal(0, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'[dbo].[CaseRepairSpecifications]')
              AND name = N'PaintLabourRate'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns AS column_value
            INNER JOIN sys.types AS type_value
                ON type_value.user_type_id = column_value.user_type_id
            WHERE column_value.object_id = OBJECT_ID(N'[dbo].[CaseRepairSpecifications]')
              AND column_value.name = N'LabourRate'
              AND type_value.name = N'decimal'
              AND column_value.[precision] = 18
              AND column_value.scale = 2
              AND column_value.is_nullable = 1
            """));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task FilteredReportGenerationIndexMigrationRefusesDowngradeWithoutChangingItsSchema()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            migrate: false,
            useTemplate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(FilterActiveReportGenerationMigration);
        var appliedBefore = await context.Database.GetAppliedMigrationsAsync();
        var pendingBefore = await context.Database.GetPendingMigrationsAsync();
        Assert.Equal(FilterActiveReportGenerationMigration, appliedBefore.Last());
        Assert.NotEmpty(pendingBefore);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(V1FoundationMigration));

        Assert.Equal(
            "This migration cannot be reverted after stale report generations may share a snapshot hash.",
            exception.Message);
        Assert.Equal(appliedBefore, await context.Database.GetAppliedMigrationsAsync());
        Assert.Equal(pendingBefore, await context.Database.GetPendingMigrationsAsync());
        Assert.Contains(FilterActiveReportGenerationMigration, appliedBefore);
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'[dbo].[CaseReportGenerations]')
              AND name = N'IX_CaseReportGenerations_CaseId_SnapshotHash'
              AND is_unique = 1
              AND REPLACE(REPLACE(REPLACE(filter_definition, N' ', N''), N'(', N''), N')', N'')
                  = N'[State]<>N''Stale'''
            """));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static string[] ParseLines(string spec) =>
        spec.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private sealed class ConnectedContextFactory(DbContextOptions<PegasusDbContext> options)
        : IDbContextFactory<PegasusDbContext>
    {
        public PegasusDbContext CreateDbContext() => new(options);
    }

    /// <summary>A synthetic Connexus report: one readable page, no scanned pages.</summary>
    private sealed class ConnexusReportReader : IIntakeSourceReader
    {
        private const string Report = """
            Mr D Roberton                                        Date:  09/03/2026
            Gravesend                                            Our Ref:  00077570/PK

                           Engineer Repairable Report

                 Vehicle Value: £9,267.00      Repair Cost: £6,143.90 inc VAT     Roadworthy: No

            Phil Kendrick AQP CAE AMIMI
            Connexus Vehicle Assessors
            """;

        public Task<IntakeSourceReadResult> ReadAsync(IntakeSource source, CancellationToken cancellationToken) =>
            Task.FromResult(new IntakeSourceReadResult(
                IntakeSourceReadStatus.Readable,
                [new(IntakeEvidenceSource.PdfContent, $"uploaded {source.FileName}, page 1", Report)],
                [],
                [],
                RequiresOcr: false));
    }

    private sealed class FixedArtifactStore(string storageKey, byte[] content) : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream value, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(key == storageKey ? content : null);
    }

    private sealed class NoCaseDocumentMetadata : IGetCaseDocumentMetadata
    {
        public Task<CaseDocumentMetadata?> ExecuteAsync(
            GetCaseDocumentMetadataQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<CaseDocumentMetadata?>(null);
    }

    private sealed class NoLogicalDocumentVersionReader : IReadLogicalDocumentVersion
    {
        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static async Task<string[]> ReadValuesAsync(
        LocalDbTestDatabase database,
        string commandText)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }
        return values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
