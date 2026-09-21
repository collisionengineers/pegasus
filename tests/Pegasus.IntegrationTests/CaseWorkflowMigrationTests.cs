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
    private const string EditScopeRemovalPredecessor = "20260917161519_RemovePublicUploadLinks";
    private const string EditScopeRemovalMigration = "20260918090000_RemoveAdministrationEditScopes";
    private const string PrePublicUploadRemovalMigration = "20260917153000_CaseClaimSourceContactOverride";
    private const string ReviewCaseId = "60000000-0000-0000-0000-000000000001";
    private const string NotReadyCaseId = "60000000-0000-0000-0000-000000000002";

    [Fact]
    public async Task AdministrationEditScopeCleanupRemovesOnlyRetiredKindsAndFreshMigrationAppliesIt()
    {
        await using (var database = await LocalDbTestDatabase.CreateAsync(migrate: false))
        {
            await using var context = await database.CreateContextAsync();
            await context.Database.MigrateAsync(EditScopeRemovalPredecessor);
            await database.ExecuteAsync(AdministrationEditScopesSql);
            await context.Database.MigrateAsync();

            Assert.Equal(0, await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.EditScopes WHERE ScopeKind IN " +
                "(N'Contact', N'StaffAccount', N'ValuationPreset', N'ApprovedMailbox', " +
                "N'ApprovedOutlookCategory', N'NamedConfiguration', N'LabourRateCard')"));
            Assert.Equal(1, await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.EditScopes WHERE ScopeKind = N'Triage'"));
            Assert.Equal(1, await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.EditScopes WHERE ScopeKind = N'ImageIntake'"));
            Assert.Equal(2, await database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.EditScopes"));
        }

        await using var freshDatabase = await LocalDbTestDatabase.CreateAsync(
            migrate: true,
            useTemplate: false);
        await using var freshContext = await freshDatabase.CreateContextAsync();
        Assert.Equal(LocalDbSchemaOrigin.Migrated, freshDatabase.SchemaOrigin);
        Assert.Contains(
            EditScopeRemovalMigration,
            await freshContext.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await freshContext.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task SqlServerUpgradeBackfillsExistingReviewAndNotReadyCasesWithRequiredTokens()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(ExistingCasesSql(withStaffConfirmationColumns: true));
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
    public async Task PublicUploadRemovalPreservesOrdinaryCaseDocumentIntakeAndChaserRecords()
    {
        const string documentId = "80000000-0000-0000-0000-000000000001";
        const string versionId = "81000000-0000-0000-0000-000000000001";
        const string occurrenceId = "82000000-0000-0000-0000-000000000001";
        const string chaserId = "83000000-0000-0000-0000-000000000001";
        const string operationKey = "removal-migration-chaser";
        var nextChaseAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        var nextChaseTicks = nextChaseAtUtc.UtcDateTime.Ticks;
        var requestHash = new string('c', 64);

        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PrePublicUploadRemovalMigration);
        await database.ExecuteAsync(ExistingCasesSql(withStaffConfirmationColumns: false));
        await database.ExecuteAsync(
            $"""
            INSERT INTO CaseWorkflows
                (CaseId, State, Version, EditLeaseGeneration, ConcurrencyToken)
            VALUES
                ('{NotReadyCaseId}', 'NotReady', 0, 0, NEWID());
            INSERT INTO CaseDueWork
                (CaseId, MissingMaterialReason, State, NextChaseAtUtc,
                 NextChaseAtUtcTicks, Version, ConcurrencyToken)
            VALUES
                ('{NotReadyCaseId}', N'Awaiting evidence', 'Scheduled',
                 '{nextChaseAtUtc:O}', {nextChaseTicks}, 0, NEWID());
            INSERT INTO CaseDueChasers
                (Id, CaseId, ScheduledAtUtc, GeneratedAtUtc, NextChaseAtUtc,
                 CopyableText, OperationKey, RequestHash, BeforeDueWorkVersion,
                 AfterDueWorkVersion)
            VALUES
                ('{chaserId}', '{NotReadyCaseId}', '{nextChaseAtUtc.AddDays(-7):O}',
                 '{nextChaseAtUtc.AddDays(-7):O}', '{nextChaseAtUtc:O}',
                 N'Please send the outstanding evidence.', '{operationKey}',
                 '{requestHash}', 0, 1);
            INSERT INTO CaseDocuments (Id, CaseId, Ordinal, SourceOccurrenceIdentity)
            VALUES ('{documentId}', '{NotReadyCaseId}', 1, 'migration:retained-document');
            INSERT INTO DocumentVersions
                (Id, DocumentId, Version, FileName, MediaType, ContentLength,
                 Sha256, CustodyStatus, CreatedAtUtc, CreatedBy, IsCurrent,
                 IsLogicallyRemoved)
            VALUES
                ('{versionId}', '{documentId}', 1, 'retained-evidence.jpg',
                 'image/jpeg', 1,
                 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
                 'Pending', '{nextChaseAtUtc:O}', 'migration', 1, 0);
            INSERT INTO DocumentOccurrences
                (Id, CaseId, DocumentId, VersionId, Ordinal, SemanticRole, Source,
                 SourceOccurrenceIdentity, RecordedAtUtc, OperationKey,
                 RotationDegrees, PreparationVersion)
            VALUES
                ('{occurrenceId}', '{NotReadyCaseId}', '{documentId}', '{versionId}',
                 1, 'Image', 'StaffUpload', 'migration:retained-document',
                 '{nextChaseAtUtc:O}', 'migration:retained-document', 0, 0);
            """);

        Assert.Equal(4, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'RequestUploadLinks', N'RequestUploadReceipts', N'PublicUploadSessions', N'PublicUploadOccurrences')"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CaseDueChasers') AND name IN (N'RequestLinkReference', N'RequestLinkPurpose')"));

        await context.Database.MigrateAsync();

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'RequestUploadLinks', N'RequestUploadReceipts', N'PublicUploadSessions', N'PublicUploadOccurrences')"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CaseDueChasers') AND name IN (N'RequestLinkReference', N'RequestLinkPurpose')"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM Cases WHERE Id = '{NotReadyCaseId}'"));
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM IntakeReceipts"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDocuments WHERE Id = '{documentId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentVersions WHERE Id = '{versionId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM DocumentOccurrences WHERE Id = '{occurrenceId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseDueChasers WHERE Id = '{chaserId}' AND OperationKey = '{operationKey}'"));
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
        await database.ExecuteAsync(ExistingCasesSql(withStaffConfirmationColumns: true));
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
        // The hand-off's three tables were dropped. This case still
        // proves the migration chain runs to completion over pre-existing
        // rows; the table it used to look for is the evidence that it did.
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'EvaHandoffDownloadOperations'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = 'EvaFirstHandoffProxies'"));
        Assert.Equal(
            [
                "20260906054658_V1PlatformFoundation",
                "20260906170000_FilterActiveCaseReportGenerationSnapshot",
                "20260906212604_RetainedMailboxReplyTargets",
                "20260906220638_RemovePaintLabourRate",
                "20260907093000_PublicUploadOccurrenceReplacementLineage",
                "20260907100000_RemoveAutomaticEvaSubmission",
                "20260907210000_ReportInputInvalidationPermissions",
                "20260907221500_RemoveCaseStaffConfirmation",
                "20260909091500_RemoveCaseDocumentOcrOperations",
                "20260909120000_ApprovedMailboxDefaultStaffSend",
                "20260909140000_EditScopeOwnership",
                "20260909141000_Contacts",
                "20260909142000_StaffAccountSingleRoleAndVersion",
                "20260909143000_EditableWorkflowConfiguration",
                "20260909144000_PrincipalReportGenerationPolicies",
                "20260909145000_GuidanceAndRemoveEngineerNotes",
                "20260909146000_ManualCaseCreation",
                "20260909147000_RemoveClaimSourceCaseNote",
                "20260909148000_CanonicalContactLocations",
                "20260910100000_ManualUploadGroupDiscard",
                "20260910101000_RemoveManualChaseReason",
                "20260910102000_AllowInitialIntakeAssociationWithoutReason",
                "20260910103000_RetainObservedStaffMailSentEvidence",
                "20260910104000_RecordCaseReportViewAndDownloadEvents",
                "20260910105000_SoftRemoveValuationPresets",
                "20260910110000_SecurityEventActingPrincipal",
                "20260910111500_DocumentContentCacheVariants",
                "20260910120000_CaseImageTags",
                "20260911100000_PromoteVehicleLookupSuggestionsToFacts",
                "20260913082540_WorkflowDueTargets",
                "20260913083338_CaseHoldReviewDate",
                "20260913090000_MarketResearchImageTag",
                "20260913090111_OrganizationNotesOnEveryCase",
                "20260913091041_RetainedMailDismissal",
                "20260913092038_StaffNotifications",
                "20260913092816_AiJobDraftReadyAt",
                "20260913094324_WorkCentreLastSeen",
                "20260913095127_UnidentifiedCouldNotBeRead",
                "20260913101413_LinkedAuditCase",
                "20260913200000_CaseFieldProposals",
                "20260914091000_PreCaseImagePreparation",
                "20260914100000_WidenDocumentContentCacheVariant",
                "20260914150656_UploadedCorrespondenceMailbox",
                "20260916090000_VehicleLookupTypeSignals",
                "20260917014000_EstimateDocumentPreviewEvents",
                "20260917140000_GrantWorkerCaseAssessmentFields",
                "20260917150000_RemoveCaseSequenceCeiling",
                "20260917152000_CaseDueByStaffOverride",
                "20260917153000_CaseClaimSourceContactOverride",
                "20260917161519_RemovePublicUploadLinks",
                "20260918090000_RemoveAdministrationEditScopes",
                "20260921060711_CapValuationSource",
                "20260921070000_DamageImpactsAsAreas"
            ],
            await context.Database.GetPendingMigrationsAsync());
    }

    private const string AdministrationEditScopesSql =
        """
        INSERT INTO [dbo].[EditScopes]
            ([ScopeKind], [RecordId], [HolderKind], [Holder], [TokenHash],
             [ExpectedVersion], [Generation], [ExpiresAtUtc])
        SELECT [ScopeKind], [RecordId], N'Staff', N'administration-migration-test',
               REPLICATE(N'A', 64), 0, 1, '2031-05-06T10:30:00+00:00'
        FROM (VALUES
            (N'Contact', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000001')),
            (N'StaffAccount', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000002')),
            (N'ValuationPreset', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000003')),
            (N'ApprovedMailbox', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000004')),
            (N'ApprovedOutlookCategory', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000005')),
            (N'NamedConfiguration', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000006')),
            (N'LabourRateCard', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000007')),
            (N'Triage', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000008')),
            (N'ImageIntake', CONVERT(uniqueidentifier, '81000000-0000-0000-0000-000000000009'))
        ) AS scopes([ScopeKind], [RecordId]);
        """;

    /// <summary>
    /// Seed rows for an upgrade test. The two staff-confirmation columns exist
    /// only in schemas before 20260907221500_RemoveCaseStaffConfirmation, so a
    /// test that starts from a later schema omits them.
    /// </summary>
    private static string ExistingCasesSql(bool withStaffConfirmationColumns)
    {
        var staffConfirmationColumns = withStaffConfirmationColumns
            ? "InstructionConfirmedByStaff, ImagesConfirmedByStaff, "
            : string.Empty;
        var staffConfirmationValues = withStaffConfirmationColumns
            ? "1, 1, "
            : string.Empty;
        return $$"""
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
             'WMIG',
             '30000000-0000-0000-0000-000000000001',
             NULL,
             NULL,
             1,
             0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete,
             {{staffConfirmationColumns}}CreatedAtUtc, Version,
             ConcurrencyToken)
        VALUES
            ('60000000-0000-0000-0000-000000000001',
             '40000000-0000-0000-0000-000000000001',
             '30000000-0000-0000-0000-000000000001',
             2031,
             1,
             'WMIG31001',
             'inspection',
             'review',
             'pending',
             '50000000-0000-0000-0000-000000000001',
             1,
             1,
             {{staffConfirmationValues}}'2031-05-06T10:30:00+00:00',
             0,
             '70000000-0000-0000-0000-000000000001'),
            ('60000000-0000-0000-0000-000000000002',
             '40000000-0000-0000-0000-000000000001',
             '30000000-0000-0000-0000-000000000001',
             2031,
             2,
             'WMIG31002',
             'inspection',
             'not_ready',
             'pending',
             '50000000-0000-0000-0000-000000000002',
             1,
             0,
             {{staffConfirmationValues}}'2031-05-06T10:31:00+00:00',
             0,
             '70000000-0000-0000-0000-000000000002');
        """;
    }

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
