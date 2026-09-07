using System.Globalization;
using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class IntakePersistenceIntegrationTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task CommittedMigrationCreatesTheSqlServerSchema()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);

        await database.MigrateAsync();

        await using var context = await database.CreateContextAsync();
        Assert.Equal(
            [
                "20260724104624_InitialProviderNeutralIntake",
                "20260727170804_ProviderDomainReferenceSnapshotV1",
                "20260729150000_DocumentCustodyAndRequests",
                "20260729152105_WorkflowTriageEmailEvidence",
                "20260729160000_CaseWorkflowRuntime",
                "20260729170000_MailboxRouteAudit",
                "20260729171000_CaseAcceptanceReplay",
                "20260729172000_CaseHoldState",
                "20260729173000_AuditIdentityEvidence",
                "20260729174000_MailboxPoisonRecovery",
                "20260729175000_CaseEvidenceAndReplacement",
                "20260729176000_AzureSqlRuntimeLeastPrivilege",
                "20260729180000_AdministrationPolicies",
                "20260729181000_VehicleWorkflow",
                "20260729182000_EvaHandoffPersistence",
                "20260729183000_SentEvidencePolling",
                "20260729184000_DueChaserSweep",
                "20260729185000_TypedCaseDataCompleteness",
                "20260729186000_CaseTasksArchive",
                "20260729187000_OrganizationPrincipalAdministration",
                "20260729188000_IntakeResolutionAndAssociations",
                "20260729189000_IdentityBootstrapAndOAuthAdministration",
                "20260729190000_CaseEditLeaseReplay",
                "20260729191000_OperationsProjectionIndexes",
                "20260729192000_TriageReplaySnapshots",
                "20260729193000_UniqueTriageResponseEvidenceLink",
                "20260729199000_RuntimeRoleReconciliation",
                "20260730203141_ThirdPartyVehicleEvidenceAndRemoveBootstrap",
                "20260730203833_RemoveDormantOpenIddict",
                "20260801220500_GrantWebMigrationHistoryRead",
                "20260803014608_ProviderInspectionModeSetting",
                "20260803071539_ImageIntakeRegistration",
                "20260803123935_MailClassificationDecisions",
                "20260803125915_CaseMatchDecisionsAndAssociationPolicy",
                "20260803151159_AutomationActorOpenIddict",
                "20260803205759_SendToAiAssessmentToolset",
                "20260805210236_ApprovedMailboxGraphIdentity",
                "20260805223036_RetainedMailboxMessages",
                "20260806090000_ApprovedInboxPollStateIdentityAdoption",
                "20260811063940_QdosAllocationRecovery",
                "20260811122654_CaseCustodyEvaRecovery",
                "20260812010335_ManualInspectionAuditCustody",
                "20260813025241_StandaloneAuditReportDecision",
                "20260814092852_AddWorkerCaseCreationGrants",
                "20260814094632_DropBoxFileRequests",
                "20260819093019_RetainedMailboxInternetMessageIdentity",
                "20260819101344_GroupedIntakeSubmission",
                "20260819104953_MailClassificationCorrectionHistory",
                "20260819112640_VersionedRepairSpecifications",
                "20260819112914_ImageInitiatedLifecycle",
                "20260819115323_UnidentifiedWork",
                "20260819140113_ImageIntakeGroupExpectedMemberCount",
                "20260819180000_GrantEvaHandoffDownloadOperations",
                "20260819234014_GrantWorkerIntakeSubmissionGroupRead",
                "20260820034652_ImageIntakeSubmissionGroup",
                "20260820040337_SendToAiConnectorSettings",
                "20260820055900_ImageCaseCustody",
                "20260820100056_ApprovedMailboxLogicalFolderBindings",
"20260820100724_RetainedMailSearchDocuments",
                "20260820114412_ApprovedOutlookCategoryCatalogue",
                "20260820144004_RetainedMailFolderMoves",
                "20260821095500_GrantWorkerVehicleLookupRequests",
                "20260821100623_GrantImageIntakeLifecycleUpdates",
                "20260822044425_GrantWorkerCaseDocuments",
                "20260822195419_CorrectIntakePhotographSemanticRole",
                "20260822223626_BackfillVehicleLookupSuggestions",
                "20260824090400_DropEvaHandoffProvenanceAndManifest",
                "20260824123336_DropEvaHandoffTables",
                "20260825001401_RemoveWorkflowCompletenessWaivers",
                "20260825105037_AssessmentAccessExportVersion",
                "20260825121453_GrantWorkerImageIntakeLifecycleEvents",
                "20260825145216_MailboxImageIntake",
                "20260826151807_ApprovedMailboxStableIdentityAndSubscriptions",
                "20260827100901_ReactivateBoundApprovedMailboxes",
                "20260827143132_EvaApiSubmissions",
                "20260827143200_GrantEvaSubmissions",
                "20260828084601_AiJobs",
                "20260828084644_GrantAiJobs",
                "20260828104130_PrincipalApiCredentials",
                "20260828104139_GrantPrincipalApiCredentials",
                "20260828110108_CaseEditLeaseHolderKind",
                "20260828111707_ProviderSubmissions",
                "20260828111732_GrantProviderSubmissions",
                "20260828112103_NamedEstimates",
                "20260828185508_ProviderDeclaredInstruction",
                "20260829095336_CaseValuations",
                "20260829212237_GrantProviderSubmissionAcceptRecovery",
                "20260903110926_ExtendAssessmentVocabulary",
                "20260903153134_RemoveStaffReviewFlags",
                "20260903225331_StaffAccountSignOff",
                "20260903233954_MarketResearchAiJob",
                "20260904210022_EngineerNotes",
                "20260904233144_CaseInspectionAddressChoices",
                "20260905010654_CaseSignOffEngineer",
                "20260906054658_V1PlatformFoundation",
                "20260906170000_FilterActiveCaseReportGenerationSnapshot",
                "20260906212604_RetainedMailboxReplyTargets",
                "20260906220638_RemovePaintLabourRate",
                "20260907093000_PublicUploadOccurrenceReplacementLineage"
            ],
            (await context.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        // AUTO-012's accept-recovery joins compare SQL Server's uniqueidentifier
        // conversion, which is UPPERCASE, against tokens .NET wrote lowercase
        // (Guid.ToString("N") for ExternalReceiptToken, "D" for AggregateId).
        // They therefore match only under a case-insensitive collation, and
        // nothing else in the repository pins that: there is no COLLATE in the
        // context or any migration, and the Bicep declares no database
        // collation, so production inherits the Azure default.
        //
        // If that ever became case-sensitive or binary, accept recovery would
        // become a permanent silent no-op — invisible even to the FirstFailure
        // diagnostics, because no exception is raised — and the ActionHistory
        // join would make every retained submission a permanent candidate,
        // reintroducing the bounded-window starvation this ticket removed.
        // Pinned here so the assumption fails loudly instead of silently.
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT CASE WHEN CONVERT(sysname, DATABASEPROPERTYEX(DB_NAME(), 'Collation'))"
            + " COLLATE Latin1_General_CI_AS LIKE '%[_]CI[_]%' THEN 1 ELSE 0 END"));

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ApprovedOutlookCategories'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ApprovedMailboxFolderBindings'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseAssessmentFields'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseEstimateLines'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseRepairSpecifications'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'AiWorkRequests'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'SendToAiControl'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictApplications'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictAuthorizations'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictScopes'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'OpenIddictTokens'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ImageIntakes'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ImageIntakeSequences'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ImageVrmSuggestions'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeReceipts'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeSearchDocuments'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeAssets'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'InstructionDrafts'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'IntakeReceiptEvents'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ProviderDomainPackages'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ProviderReferences'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'ProviderDomainEvidence'"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderDomainPackages"));
        Assert.Equal(11, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderReferences"));
        Assert.Equal(16, await database.ScalarAsync<int>("SELECT COUNT(*) FROM ProviderDomainEvidence"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'Cases'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseSequences'"));
        Assert.Equal(6, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Cases')
              AND name IN (
                  N'IX_Cases_AuditReference',
                  N'IX_Cases_OriginIntakeReceiptId',
                  N'IX_Cases_PrincipalId',
                  N'IX_Cases_Reference',
                  N'IX_Cases_SequenceLineageId_Year_Sequence',
                  N'IX_Cases_StandaloneAuditEvidenceId')
            """));
        Assert.Equal(6, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Cases')
              AND name LIKE N'IX_Cases[_]%'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'Cases')
              AND name = N'IX_Cases_AuditReference'
              AND is_unique = 1
              AND has_filter = 1
            """));
        Assert.Equal(3, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'CaseIntakeLinks')
              AND name IN (
                  N'ExpectedIntakeVersion',
                  N'AcceptanceCommandMaterialJson',
                  N'AcceptanceCommandFingerprint')
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseWorkflows'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseWorkflowEvents'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseDueWork'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseManualChases'"));
    }

    [Fact]
    public async Task StableMailboxMigrationPreservesRetainedEvidenceAndRestrictiveDependants()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync("20260825145216_MailboxImageIntake");

        var mailboxId = Guid.Parse("49f47eb9-c5b0-464f-b8f0-8c90ba061728");
        var messageId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var moveId = Guid.NewGuid();
        var poisonId = Guid.NewGuid();
        await database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes SET MailboxIdentity = 'legacy-graph', InboxFolderIdentity = 'inbox'
            WHERE Id = '{mailboxId:D}';
            INSERT INTO ApprovedInboxPollStates (MailboxId, MailboxAddress, [Cursor], DueAtUtc)
            VALUES ('legacy-graph', 'instructions@collisionengineers.co.uk', 'retained-cursor', '2031-05-06T10:30:00+00:00');
            INSERT INTO ApprovedInboxPoisonMessages
                (Id, MailboxId, OccurrenceKey, ImmutableMessageId, FileName, ReceivedAtUtc, FailureCode, CursorAfterMessage, QuarantinedAtUtc)
            VALUES
                ('{poisonId:D}', 'legacy-graph', '{new string('a', 64)}', 'poison-message', 'poison.eml', '2031-05-06T10:30:00+00:00', 'fixture', 'cursor-after', '2031-05-06T10:31:00+00:00');
            INSERT INTO RetainedMailboxMessages
                (Id, MailboxId, MailboxAddress, FolderScope, FolderIdentity, ImmutableMessageId, ExternalReceiptToken,
                 ToAddressesJson, CcAddressesJson, IsRead, SourceLength, SourceSha256, ReceivedAtUtc, RetainedAtUtc)
            VALUES
                ('{messageId:D}', 'legacy-graph', 'instructions@collisionengineers.co.uk', 'inbox', 'inbox', 'retained-message',
                 'retained-token', '[]', '[]', 0, 10, '{new string('b', 64)}', '2031-05-06T10:30:00+00:00', '2031-05-06T10:31:00+00:00');
            INSERT INTO RetainedMailboxAttachments
                (Id, RetainedMailboxMessageId, Ordinal, FileName, MediaType, ContentLength)
            VALUES ('{attachmentId:D}', '{messageId:D}', 0, 'evidence.pdf', 'application/pdf', 10);
            INSERT INTO RetainedMailFolderMoves
                (Id, RetainedMailboxMessageId, OperationKey, RequestHash, ExpectedClassificationVersion,
                 ExpectedRecommendationPolicyKey, ExpectedRecommendationPolicyVersion, ExpectedMailboxVersion,
                 MailboxId, ImmutableMessageId, SourceFolderId, DestinationFolderId, FolderType, Actor,
                 ActorRolesJson, Reason, Outcome, RecordedAtUtc)
            VALUES
                ('{moveId:D}', '{messageId:D}', '{Guid.NewGuid():D}', '{new string('c', 64)}', 1,
                 'fixture-policy', 1, 1, 'legacy-graph', 'retained-message', 'inbox', 'processed', 'Processed',
                 'system:fixture', '[]', 'fixture', 'pending', '2031-05-06T10:32:00+00:00');
            """);

        await context.Database.MigrateAsync();

        Assert.Equal(mailboxId, await database.ScalarAsync<Guid>(
            $"SELECT MailboxId FROM RetainedMailboxMessages WHERE Id = '{messageId:D}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM RetainedMailboxAttachments WHERE RetainedMailboxMessageId = '{messageId:D}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM RetainedMailFolderMoves WHERE RetainedMailboxMessageId = '{messageId:D}'"));
        Assert.Equal(mailboxId, await database.ScalarAsync<Guid>(
            "SELECT ApprovedMailboxId FROM ApprovedInboxPollStates"));
        Assert.Equal(mailboxId, await database.ScalarAsync<Guid>(
            "SELECT ApprovedMailboxId FROM ApprovedInboxPoisonMessages"));
    }

    /// <remarks>
    /// Intake queue counts use the persisted decision code. Needs-sorting
    /// receipts contribute to <see cref="IntakeQueueCounts.NeedsSorting"/>;
    /// blocked-intake receipts remain a separate decision and do not.
    /// </remarks>
    [Fact]
    public async Task IntakeQueueCountsUsePersistedDecisionCodes()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.StoreAsync(CreateDraft(1, IntakeDecision.NeedsSorting));
        await database.StoreAsync(CreateDraft(2, IntakeDecision.NeedsSorting));
        await database.StoreAsync(CreateDraft(3, IntakeDecision.BlockedIntake));

        var counts = await database.GetCountsAsync();

        Assert.Equal(2, counts.NeedsSorting);
    }

    [Fact]
    public async Task EightConcurrentDistinctSourceIdentitiesPersistEightPreCaseDrafts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var records = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
            database.StoreAsync(CreateDraft(index, IntakeDecision.CaseCreated))));

        Assert.Equal(8, records.Select(record => record.Id).Distinct().Count());
        Assert.All(records, record =>
        {
            Assert.NotNull(record.InstructionDraft);
        });
        Assert.Equal(8, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(8, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(8, await database.CountAsync("IntakeReceiptEvents"));
    }

    [Fact]
    public async Task EightConcurrentSameSourceIdentityCallsCreateOneReceiptAndDraft()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, IntakeDecision.CaseCreated);

        var records = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => database.StoreAsync(draft)));

        Assert.Single(records.Select(record => record.Id).Distinct());
        Assert.All(records, record =>
        {
            Assert.NotNull(record.InstructionDraft);
        });
        Assert.Equal(1, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(1, await database.CountAsync("IntakeReceiptEvents"));
    }

    [Fact]
    public async Task FailedReceiptEventInsertRollsBackReceiptAndTypedDraftBeforeRetry()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.ExecuteAsync(
            "CREATE TRIGGER [FailReceiptEventInsert] ON [dbo].[IntakeReceiptEvents] INSTEAD OF INSERT AS " +
            "BEGIN THROW 51000, 'Deliberate integration-test receipt-event failure.', 1; END");
        var draft = CreateDraft(1, IntakeDecision.CaseCreated);

        await Assert.ThrowsAsync<DbUpdateException>(() => database.StoreAsync(draft));

        Assert.Equal(0, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(0, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(0, await database.CountAsync("IntakeReceiptEvents"));
        await database.ExecuteAsync("DROP TRIGGER [dbo].[FailReceiptEventInsert]");

        var retried = await database.StoreAsync(draft);

        Assert.NotNull(retried.InstructionDraft);
        Assert.Equal(1, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("InstructionDrafts"));
        Assert.Equal(1, await database.CountAsync("IntakeReceiptEvents"));
    }

    [Fact]
    public async Task ConfirmedDraftCannotCreateCaseOrReferenceCounterPersistence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var record = await database.StoreAsync(CreateDraft(1, IntakeDecision.CaseCreated));

        Assert.NotNull(record.InstructionDraft);
        Assert.Equal(1, await database.CountAsync("IntakeReceipts"));
        Assert.Equal(1, await database.CountAsync("IntakeReceiptEvents"));
        Assert.Equal(0, await database.CountAsync("Cases"));
        Assert.Equal(0, await database.CountAsync("CaseSequences"));
    }

    [Fact]
    public async Task DraftReceiptPersistsReceiptHistoryContents()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, IntakeDecision.CaseCreated);

        var record = await database.StoreAsync(draft);
        var receiptEvent = await database.ReadSingleReceiptEventAsync();

        Assert.Equal(record.Id, receiptEvent.IntakeReceiptId);
        Assert.Equal("intake_receipt_recorded", receiptEvent.EventType);
        Assert.Equal("LocalDB integration test", receiptEvent.Actor);
        Assert.Equal(FixedTime, receiptEvent.OccurredAtUtc);
        using var details = JsonDocument.Parse(receiptEvent.DetailsJson);
        Assert.Equal(1, details.RootElement.GetProperty("version").GetInt32());
        var data = details.RootElement.GetProperty("data");
        Assert.Equal("case_created", data.GetProperty("decision").GetString());
        Assert.Equal("manual_upload", data.GetProperty("sourceChannel").GetString());
        Assert.Equal(draft.SourceIdentity.ExternalReceiptToken,
            data.GetProperty("externalReceiptToken").GetString());
        Assert.False(data.TryGetProperty("caseReference", out _));
        Assert.False(data.TryGetProperty("caseCreationAuthorized", out _));
        Assert.Equal(draft.SourceHash, data.GetProperty("sourceHash").GetString());
    }

    [Fact]
    public async Task NeedsSortingFilterReturnsOnlyLiteralNeedsSortingReceipts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await database.StoreAsync(CreateDraft(1, IntakeDecision.NeedsSorting));
        await database.StoreAsync(CreateDraft(2, IntakeDecision.OcrRequired));
        await database.StoreAsync(CreateDraft(3, IntakeDecision.Unsupported));
        await database.StoreAsync(CreateDraft(4, IntakeDecision.TechnicalFailure));

        var result = await database.ListAsync(IntakeDecision.NeedsSorting);

        var receipt = Assert.Single(result);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
        Assert.Equal("source-1.bin", receipt.SourceFileName);
        Assert.Equal(new IntakeQueueCounts(1, 0), await database.GetCountsAsync());
    }

    [Fact]
    public async Task StandaloneAuditReportSurvivesTheReceiptRoundTrip()
    {
        // Regression for the Audit auto-create defect: the standalone Audit
        // report evaluation must survive the receipt persistence round-trip, or
        // the retry-from-durable-receipt replay records no Audit evidence and no
        // Audit case can ever mint. A real EF round-trip is required because the
        // unit test double returned the same in-memory object and hid this.
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var draft = CreateDraft(1, IntakeDecision.CaseCreated) with
        {
            MailClassificationDecision = MailClassificationResult.Classified(
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "audit"),
                [],
                "A standalone Audit instruction was identified.",
                "qdos-mail-classification",
                1,
                CaseType.Audit,
                new StandaloneAuditReportEvaluation(
                    "uploaded original.eml, attachment 1: original-report.pdf",
                    AuditAssessment.Repairable))
        };

        var stored = await database.StoreAsync(draft);
        var readBack = await database.GetReceiptAsync(stored.Id);

        Assert.NotNull(readBack);
        Assert.NotNull(readBack!.MailClassificationDecision);
        var report = readBack.MailClassificationDecision!.StandaloneAuditReport;
        Assert.NotNull(report);
        Assert.Equal(
            "uploaded original.eml, attachment 1: original-report.pdf",
            report!.AssetSourceLabel);
        Assert.Equal(AuditAssessment.Repairable, report.Assessment);
    }

    private static IntakeReceiptDraft CreateDraft(
        int id,
        IntakeDecision decision) => new(
        $"source-{id}.bin",
        "application/octet-stream",
        id,
        id.ToString("X64", CultureInfo.InvariantCulture),
        new(IntakeSourceChannel.ManualUpload, id.ToString("x32", CultureInfo.InvariantCulture)),
        FixedTime,
        FixedTime,
        "LocalDB integration test",
        decision,
        $"{decision} integration-test decision",
        [new(IntakeEvidenceSource.SystemDefault, IntakeEvidenceStrength.Weak, IntakeEvidenceFinding.Information,
            "integration-test", "Persistence boundary evidence")],
        [new("Instruction date", "2031-05-06", [], true, false)],
        decision == IntakeDecision.CaseCreated
            ? new("QDOS", null, null, null, null, null, null, null, null, new DateOnly(2031, 5, 6), null)
            : null,
        [],
        null,
        null,
        "controlled_test_reader",
        "1",
        decision == IntakeDecision.CaseCreated ? QdosInstructionExtractionPolicy.Key : null,
        decision == IntakeDecision.CaseCreated ? QdosInstructionExtractionPolicy.Version : null);
}

/// <summary>
/// Where a disposable test database's schema came from.
/// </summary>
internal enum LocalDbSchemaOrigin
{
    /// <summary>No migration was applied.</summary>
    Empty,

    /// <summary>The migration stream was applied to this database.</summary>
    Migrated,

    /// <summary>The once-per-run migrated template was restored.</summary>
    Template
}

internal sealed class LocalDbTestDatabase : IAsyncDisposable
{
    internal const string Prefix = "Pegasus_Test_";

    /// <summary>
    /// How long a database-lifecycle statement may take.
    /// </summary>
    /// <remarks>
    /// CREATE, RESTORE, BACKUP and DROP all queue behind one another on the
    /// instance, so with test classes running in parallel the 30-second
    /// default turns load into a failure that looks like a timeout. One
    /// number, because they contend for the same thing.
    /// </remarks>
    internal const int LifecycleCommandTimeoutSeconds = 300;

    /// <summary>
    /// Overrides the data source for the SQL Server instance these tests use.
    /// </summary>
    /// <remarks>
    /// SQL Server Express LocalDB exists only on Windows. Leaving this unset
    /// keeps the Windows default exactly as it was; setting it lets a Linux
    /// workstation point the same tests at a SQL Server container. The engine,
    /// migration stream and assertions are identical either way.
    /// </remarks>
    private const string DataSourceVariable = "PEGASUS_TEST_SQL_DATASOURCE";
    private const string UserVariable = "PEGASUS_TEST_SQL_USER";
    private const string PasswordVariable = "PEGASUS_TEST_SQL_PASSWORD";

    /// <summary>
    /// Whether <c>PEGASUS_TEST_SQL_DATASOURCE</c> points these tests at an
    /// external SQL Server instead of LocalDB.
    /// </summary>
    internal static bool UsesExternalDataSource =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataSourceVariable));

    private readonly ServiceProvider services;
    private bool disposed;

    private static string BuildConnectionString(string databaseName)
    {
        var dataSource = Environment.GetEnvironmentVariable(DataSourceVariable);
        var builder = new SqlConnectionStringBuilder
        {
            InitialCatalog = databaseName,
            // Four parallel collections (xunit.runner.json) drive DDL against
            // one LocalDB instance, so an ordinary open queues behind it: CI
            // measured 13999-14014 ms against the previous 15s budget. 60s
            // clears that and still fails a wedged instance inside the
            // 20-minute job timeout (DELIV-031).
            ConnectTimeout = 60,
            MultipleActiveResultSets = true
        };

        if (string.IsNullOrWhiteSpace(dataSource))
        {
            builder.DataSource = @"(localdb)\MSSQLLocalDB";
            builder.IntegratedSecurity = true;
            builder.Encrypt = false;
            return builder.ConnectionString;
        }

        builder.DataSource = dataSource;
        var user = Environment.GetEnvironmentVariable(UserVariable);
        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"{DataSourceVariable} is set, so {UserVariable} and {PasswordVariable} are also required.");
        }

        builder.UserID = user;
        builder.Password = password;
        builder.IntegratedSecurity = false;
        // The container presents a self-signed certificate.
        builder.Encrypt = true;
        builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }

    private LocalDbTestDatabase(
        string databaseName,
        Action<DbContextOptionsBuilder>? configureDatabase,
        Func<IServiceProvider, string>? localArtifactRootFactory,
        Action<IServiceCollection>? configureServices)
    {
        DatabaseName = databaseName;
        ConnectionString = BuildConnectionString(databaseName);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddPegasusInfrastructure(
            (_, options) =>
            {
                options.UseSqlServer(ConnectionString);
                configureDatabase?.Invoke(options);
            },
            localArtifactRootFactory);
        // This database harness has no queue transport. Production composition
        // supplies the mandatory publishers; persistence tests replace only that
        // transport boundary while exercising the durable outbox state.
        serviceCollection.AddScoped<ICommittedIntakeWorkPublisher, CommittedWorkPublisherDouble>();
        serviceCollection.AddScoped<ICommittedExternalWorkPublisher, CommittedWorkPublisherDouble>();
        configureServices?.Invoke(serviceCollection);
        services = serviceCollection.BuildServiceProvider(validateScopes: true);
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    /// <summary>
    /// How this database got its schema. A test that means to exercise the
    /// template must assert this, or a broken template is a slow pass.
    /// </summary>
    public LocalDbSchemaOrigin SchemaOrigin { get; private set; } = LocalDbSchemaOrigin.Empty;

    public SqlConnection CreateConnection() => new(ConnectionString);

    public AsyncServiceScope CreateAsyncScope() => services.CreateAsyncScope();

    public static async Task<LocalDbTestDatabase> CreateAsync(
        bool migrate = true,
        Action<DbContextOptionsBuilder>? configureDatabase = null,
        Func<IServiceProvider, string>? localArtifactRootFactory = null,
        Action<IServiceCollection>? configureServices = null,
        bool useTemplate = true)
    {
        var database = new LocalDbTestDatabase(
            Prefix + Guid.NewGuid().ToString("N"),
            configureDatabase,
            localArtifactRootFactory,
            configureServices);
        try
        {
            // An unmigrated database is what several tests are about, so the
            // template is only ever a substitute for migrating.
            var template = migrate && useTemplate
                ? await LocalDbTemplateDatabase.GetAsync()
                : null;
            if (template is not null)
            {
                await database.RestoreFromTemplateAsync(template);
                database.SchemaOrigin = LocalDbSchemaOrigin.Template;
                return database;
            }

            await database.CreateEmptyDatabaseAsync();
            if (migrate)
            {
                await database.MigrateAsync();
                database.SchemaOrigin = LocalDbSchemaOrigin.Migrated;
            }

            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async Task MigrateAsync()
    {
        await using var context = await CreateContextAsync();
        await context.Database.MigrateAsync();
    }

    public async Task<PegasusDbContext> CreateContextAsync()
    {
        var factory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        return await factory.CreateDbContextAsync();
    }

    public async Task<IntakeReceipt> StoreAsync(IntakeReceiptDraft draft)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>()
            .StoreAsync(draft, CancellationToken.None);
    }

    public async Task<IntakeReceipt?> GetReceiptAsync(Guid id)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(id, CancellationToken.None);
    }

    public async Task<IReadOnlyList<IntakeReceiptSummary>> ListAsync(IntakeDecision decision)
    {
        await using var scope = services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .ListAsync(decision, 1, 100, CancellationToken.None)).Items;
    }

    public async Task<IntakeQueueCounts> GetCountsAsync()
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetCountsAsync(CancellationToken.None);
    }

    public Task<int> CountAsync(string tableName)
    {
        var allowed = tableName switch
        {
            "IntakeReceipts" or "InstructionDrafts" or "IntakeReceiptEvents"
                or "Cases" or "CaseSequences" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        return ScalarAsync<int>($"SELECT COUNT(*) FROM [{allowed}]");
    }

    public async Task<T> ScalarAsync<T>(string commandText)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return value is T result
            ? result
            : (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    public async Task ExecuteAsync(string commandText)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<PersistedReceiptEvent> ReadSingleReceiptEventAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT IntakeReceiptId, EventType, Actor, OccurredAtUtc, DetailsJson FROM IntakeReceiptEvents";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var result = new PersistedReceiptEvent(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTimeOffset>(3),
            reader.GetString(4));
        Assert.False(await reader.ReadAsync());
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await services.DisposeAsync();
        ValidateExactDisposableName(DatabaseName);

        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();
        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText =
                $"IF DB_ID(@databaseName) IS NOT NULL BEGIN " +
                $"ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{DatabaseName}]; END";
            drop.Parameters.AddWithValue("@databaseName", DatabaseName);
            drop.CommandTimeout = LifecycleCommandTimeoutSeconds;

            // CI hit 5061 here -- "a lock could not be placed ... Try again
            // later" -- with a parallel collection holding the instance.
            // Retry is the documented remedy; 10s of backoff, then the fifth
            // attempt rethrows (DELIV-031).
            const int lockNotPlacedErrorNumber = 5061;
            const int maximumAttempts = 5;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await drop.ExecuteNonQueryAsync();
                    break;
                }
                catch (SqlException exception)
                    when (exception.Number == lockNotPlacedErrorNumber
                        && attempt < maximumAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(attempt));
                }
            }
        }

        await using var verify = connection.CreateCommand();
        verify.CommandText = "SELECT DB_ID(@databaseName)";
        verify.Parameters.AddWithValue("@databaseName", DatabaseName);
        Assert.Equal(DBNull.Value, await verify.ExecuteScalarAsync());
    }

    private async Task CreateEmptyDatabaseAsync()
    {
        ValidateExactDisposableName(DatabaseName);
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{DatabaseName}]";
        command.CommandTimeout = LifecycleCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    private async Task RestoreFromTemplateAsync(LocalDbTemplateSnapshot template)
    {
        // The restore creates the database, so it carries the same name guard
        // the create path carries.
        ValidateExactDisposableName(DatabaseName);
        await using var connection = new SqlConnection(MasterConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"RESTORE DATABASE [{DatabaseName}] FROM DISK = @backupPath WITH " +
            "MOVE @dataLogicalName TO @dataFile, MOVE @logLogicalName TO @logFile, RECOVERY";
        command.Parameters.AddWithValue("@backupPath", template.BackupPath);
        command.Parameters.AddWithValue("@dataLogicalName", template.DataLogicalName);
        command.Parameters.AddWithValue(
            "@dataFile",
            LocalDbTemplateDatabase.Combine(template.DataDirectory, DatabaseName + ".mdf"));
        command.Parameters.AddWithValue("@logLogicalName", template.LogLogicalName);
        command.Parameters.AddWithValue(
            "@logFile",
            LocalDbTemplateDatabase.Combine(template.DataDirectory, DatabaseName + "_log.ldf"));
        command.CommandTimeout = LifecycleCommandTimeoutSeconds;
        await command.ExecuteNonQueryAsync();
    }

    internal static string MasterConnectionString() => BuildConnectionString("master");

    /// <summary>
    /// The exact shape of a disposable test database's name.
    /// </summary>
    /// <remarks>
    /// One definition, because every create, restore, and drop is guarded by
    /// it. Anything failing this rule belongs to someone else and is never
    /// touched.
    /// </remarks>
    internal static bool IsDisposableName(string databaseName) =>
        databaseName.StartsWith(Prefix, StringComparison.Ordinal)
        && databaseName.Length == Prefix.Length + 32
        && Guid.TryParseExact(databaseName[Prefix.Length..], "N", out _);

    private static void ValidateExactDisposableName(string databaseName) =>
        Assert.True(
            IsDisposableName(databaseName),
            $"'{databaseName}' is not a disposable test database name.");
}

internal sealed record PersistedReceiptEvent(
    Guid IntakeReceiptId,
    string EventType,
    string Actor,
    DateTimeOffset OccurredAtUtc,
    string DetailsJson);
