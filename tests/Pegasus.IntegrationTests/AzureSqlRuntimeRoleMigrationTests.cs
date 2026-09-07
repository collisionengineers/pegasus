using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AzureSqlRuntimeRoleMigrationTests
{
    private const string PreRuntimeRoleMigration = "20260729175000_CaseEvidenceAndReplacement";
    private const string OriginalRuntimeRoleMigration = "20260729176000_AzureSqlRuntimeLeastPrivilege";
    private const string PreviousMigration = "20260729193000_UniqueTriageResponseEvidenceLink";
    private const string RuntimeRoleMigration = "20260729199000_RuntimeRoleReconciliation";
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    private const string ExpectedSchemaTableSpec = """
        ActionHistory
        ApplicationInitializations
        ApprovedInboxPoisonMessages
        ApprovedInboxPollStates
        ApprovedMailboxes
        ApprovedSentPollOutcomes
        ApprovedSentPollStates
        AspNetRoleClaims
        AspNetRoles
        AspNetUserClaims
        AspNetUserLogins
        AspNetUserRoles
        AspNetUserTokens
        AspNetUsers
        BoxFileRequests
        CaseDataFields
        CaseDataSnapshots
        CaseDocuments
        CaseDueChasers
        CaseDueWork
        CaseEditLeaseOperations
        CaseEngineerFindings
        CaseHistory
        CaseIntakeLinks
        CaseManualChases
        CaseReportApprovals
        CaseReportSentEvidence
        CaseSequences
        CaseTasks
        CaseWorkflowEvents
        CaseWorkflows
        Cases
        DocumentOccurrences
        DocumentVersions
        EmailResponseEvidence
        EvaFirstHandoffProxies
        EvaHandoffOperations
        EvaHandoffRevisions
        ExternalWorkItems
        InstructionDrafts
        IntakeAssets
        IntakeEvaluations
        IntakeMailRouteDecisions
        IntakeManualAssociations
        IntakeMutationHistory
        IntakeReceiptEvents
        IntakeReceipts
        IntakeStagedReceipts
        IntakeWorkItems
        OpenIddictApplications
        OpenIddictAuthorizations
        OpenIddictScopes
        OpenIddictTokens
        OrganizationAdministrationOperations
        OrganizationRoles
        Organizations
        PrincipalSequenceLineages
        Principals
        ProviderDomainEvidence
        ProviderDomainPackages
        ProviderReferences
        RequestUploadLinks
        RequestUploadReceipts
        SecurityEvents
        SentEmailEvidence
        StandaloneAuditEvidence
        Triage
        TriageFindings
        TriageHistory
        TriageResponseEvidenceLinks
        VehicleConfirmations
        VehicleLookupObservations
        VehicleLookupRequests
        WorkflowConfigurations
        """;

    private const string ExpectedWebGrantSpec = """
        ActionHistory:SELECT,INSERT
        ApprovedInboxPoisonMessages:SELECT
        ApprovedInboxPollStates:SELECT,UPDATE
        ApprovedMailboxes:SELECT,INSERT,UPDATE
        ApprovedSentPollOutcomes:SELECT
        ApprovedSentPollStates:SELECT,UPDATE
        AspNetRoleClaims:SELECT
        AspNetRoles:SELECT
        AspNetUserClaims:SELECT
        AspNetUserRoles:SELECT,INSERT,DELETE
        AspNetUsers:SELECT,INSERT,UPDATE
        BoxFileRequests:SELECT,INSERT,UPDATE
        CaseDataFields:SELECT,INSERT,UPDATE,DELETE
        CaseDataSnapshots:SELECT,INSERT
        CaseDocuments:SELECT,INSERT
        CaseDueChasers:SELECT
        CaseDueWork:SELECT,INSERT,UPDATE
        CaseEditLeaseOperations:SELECT,INSERT
        CaseEngineerFindings:SELECT,INSERT
        CaseHistory:SELECT,INSERT
        CaseIntakeLinks:SELECT,INSERT
        CaseManualChases:SELECT,INSERT
        CaseReportApprovals:SELECT,INSERT
        CaseReportSentEvidence:SELECT,UPDATE
        CaseSequences:SELECT,INSERT,UPDATE
        CaseTasks:SELECT,INSERT,UPDATE
        CaseWorkflowEvents:SELECT,INSERT
        CaseWorkflows:SELECT,INSERT,UPDATE
        Cases:SELECT,INSERT,UPDATE
        DocumentOccurrences:SELECT,INSERT
        DocumentVersions:SELECT,INSERT,UPDATE
        EmailResponseEvidence:SELECT
        EvaFirstHandoffProxies:SELECT,INSERT
        EvaHandoffOperations:SELECT,INSERT
        EvaHandoffRevisions:SELECT,INSERT
        ExternalWorkItems:SELECT,INSERT,UPDATE
        InstructionDrafts:SELECT,INSERT,UPDATE
        IntakeAssets:SELECT
        IntakeEvaluations:SELECT
        IntakeMailRouteDecisions:SELECT
        IntakeManualAssociations:SELECT,INSERT,UPDATE
        IntakeMutationHistory:SELECT,INSERT
        IntakeReceiptEvents:INSERT
        IntakeReceipts:SELECT,UPDATE
        IntakeStagedReceipts:SELECT,INSERT
        IntakeWorkItems:SELECT,INSERT,UPDATE
        OpenIddictApplications:SELECT,INSERT,UPDATE
        OpenIddictAuthorizations:SELECT,INSERT,UPDATE
        OpenIddictScopes:SELECT
        OpenIddictTokens:SELECT,INSERT,UPDATE
        OrganizationAdministrationOperations:SELECT,INSERT
        OrganizationRoles:SELECT,INSERT,DELETE
        Organizations:SELECT,INSERT,UPDATE
        PrincipalSequenceLineages:SELECT,INSERT
        Principals:SELECT,INSERT,UPDATE
        RequestUploadLinks:SELECT,INSERT,UPDATE
        RequestUploadReceipts:SELECT,INSERT
        SecurityEvents:SELECT,INSERT
        SentEmailEvidence:SELECT
        StandaloneAuditEvidence:SELECT,INSERT
        Triage:SELECT,UPDATE
        TriageFindings:SELECT,INSERT
        TriageHistory:SELECT,INSERT
        TriageResponseEvidenceLinks:SELECT,INSERT,DELETE
        VehicleConfirmations:SELECT,INSERT
        VehicleLookupObservations:SELECT
        VehicleLookupRequests:SELECT,INSERT
        WorkflowConfigurations:SELECT,UPDATE
        """;

    private const string ExpectedWorkerGrantSpec = """
        ActionHistory:SELECT,INSERT
        ApprovedInboxPoisonMessages:SELECT,INSERT
        ApprovedInboxPollStates:SELECT,INSERT,UPDATE
        ApprovedMailboxes:SELECT
        ApprovedSentPollOutcomes:SELECT,INSERT
        ApprovedSentPollStates:SELECT,INSERT,UPDATE
        CaseDueChasers:SELECT,INSERT,UPDATE
        CaseDueWork:SELECT,UPDATE
        CaseEditLeaseOperations:SELECT
        CaseHistory:INSERT
        CaseIntakeLinks:SELECT
        CaseReportApprovals:SELECT
        CaseReportSentEvidence:SELECT,INSERT,UPDATE
        CaseWorkflowEvents:SELECT,INSERT
        CaseWorkflows:SELECT,UPDATE
        Cases:SELECT,UPDATE
        EmailResponseEvidence:SELECT,INSERT
        ExternalWorkItems:SELECT,UPDATE
        InstructionDrafts:SELECT,INSERT,UPDATE
        IntakeAssets:SELECT,INSERT
        IntakeEvaluations:SELECT,INSERT
        IntakeMailRouteDecisions:SELECT,INSERT,UPDATE
        IntakeManualAssociations:SELECT
        IntakeReceiptEvents:INSERT
        IntakeReceipts:SELECT,INSERT,UPDATE
        IntakeStagedReceipts:SELECT,INSERT,UPDATE
        IntakeWorkItems:SELECT,INSERT,UPDATE
        ProviderDomainEvidence:SELECT
        ProviderDomainPackages:SELECT
        ProviderReferences:SELECT
        RequestUploadLinks:SELECT
        SentEmailEvidence:SELECT,INSERT,UPDATE
        Triage:SELECT,INSERT,UPDATE
        TriageHistory:SELECT,INSERT
        TriageResponseEvidenceLinks:SELECT,INSERT
        VehicleLookupObservations:INSERT
        VehicleLookupRequests:SELECT
        """;

    private const string ExpectedWebDeleteTableSpec = """
        AspNetUserRoles
        CaseDataFields
        OrganizationRoles
        TriageResponseEvidenceLinks
        """;

    private const string FoundationTableSpec = """
        AppliedValuationSnapshots
        CaseReportDeliveryIntents
        CaseReportGenerations
        ClaimSources
        DocumentContentCacheEntries
        GeneratedCaseArtifacts
        GlassRepairEstimateSessions
        IntakeOcrOperations
        IntakeSourceCandidates
        LabourRateCards
        OrganizationDirectoryEntries
        PublicUploadOccurrences
        PublicUploadSessions
        RetainedInstructionAnalyses
        StaffMailSendOperations
        TriageSequences
        UserExternalCredentials
        ValuationPresets
        """;

    private const string FoundationWebGrantSpec = """
        AppliedValuationSnapshots:SELECT,INSERT
        CaseReportDeliveryIntents:SELECT,INSERT,UPDATE
        CaseReportGenerations:SELECT,INSERT
        ClaimSources:SELECT,INSERT,UPDATE
        DocumentContentCacheEntries:SELECT,INSERT,UPDATE
        GeneratedCaseArtifacts:SELECT,INSERT
        GlassRepairEstimateSessions:SELECT,INSERT,UPDATE
        IntakeOcrOperations:SELECT,INSERT
        IntakeSourceCandidates:SELECT
        LabourRateCards:SELECT,INSERT,UPDATE
        OrganizationDirectoryEntries:SELECT,INSERT,UPDATE
        PublicUploadOccurrences:SELECT,INSERT,UPDATE
        PublicUploadSessions:SELECT,INSERT,UPDATE
        RetainedInstructionAnalyses:SELECT
        StaffMailSendOperations:SELECT,INSERT,UPDATE
        TriageSequences:SELECT,INSERT,UPDATE
        UserExternalCredentials:SELECT,INSERT,UPDATE
        ValuationPresets:SELECT,INSERT,UPDATE
        """;

    private const string FoundationWorkerGrantSpec = """
        DocumentContentCacheEntries:SELECT,INSERT,UPDATE,DELETE
        IntakeOcrOperations:SELECT,INSERT,UPDATE
        IntakeSourceCandidates:SELECT,INSERT
        RetainedInstructionAnalyses:SELECT,INSERT,UPDATE
        StaffMailSendOperations:SELECT,INSERT,UPDATE
        TriageSequences:SELECT,INSERT,UPDATE
        """;

    [Fact]
    public async Task LatestMigrationKeepsBootstrapRemovedAndRestoresAutomationOpenIddictState()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

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
        Assert.Equal(
            [
                "OpenIddictApplications:D:DELETE",
                "OpenIddictApplications:G:INSERT",
                "OpenIddictApplications:G:SELECT",
                "OpenIddictApplications:G:UPDATE",
                "OpenIddictAuthorizations:D:DELETE",
                "OpenIddictAuthorizations:G:INSERT",
                "OpenIddictAuthorizations:G:SELECT",
                "OpenIddictAuthorizations:G:UPDATE",
                "OpenIddictScopes:D:DELETE",
                "OpenIddictScopes:G:SELECT",
                "OpenIddictTokens:D:DELETE",
                "OpenIddictTokens:G:INSERT",
                "OpenIddictTokens:G:SELECT",
                "OpenIddictTokens:G:UPDATE"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    tableObject.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.objects AS tableObject
                    ON tableObject.object_id = permission.major_id
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE tableObject.name IN (
                        N'OpenIddictApplications',
                        N'OpenIddictAuthorizations',
                        N'OpenIddictScopes',
                        N'OpenIddictTokens')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name = N'{WebRole}'
                ORDER BY
                    tableObject.name COLLATE DATABASE_DEFAULT,
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    permission.permission_name COLLATE DATABASE_DEFAULT
                """));
        Assert.Equal(
            [
                "OpenIddictApplications:D:DELETE",
                "OpenIddictAuthorizations:D:DELETE",
                "OpenIddictScopes:D:DELETE",
                "OpenIddictTokens:D:DELETE"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    tableObject.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.objects AS tableObject
                    ON tableObject.object_id = permission.major_id
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE tableObject.name IN (
                        N'OpenIddictApplications',
                        N'OpenIddictAuthorizations',
                        N'OpenIddictScopes',
                        N'OpenIddictTokens')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name = N'{WorkerRole}'
                ORDER BY
                    tableObject.name COLLATE DATABASE_DEFAULT,
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    permission.permission_name COLLATE DATABASE_DEFAULT
                """));
        Assert.Equal(
            3,
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
    }

    [Fact]
    public async Task LatestMigrationDropsDeadBoxRequestsAndGrantsWorkerCaseCreation()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'BoxFileRequests'"));
        Assert.Equal(
            [
                "CaseDataFields:INSERT", "CaseDataFields:SELECT", "CaseDataFields:UPDATE",
                "CaseDataSnapshots:INSERT", "CaseDataSnapshots:SELECT",
                "CaseDueWork:INSERT", "CaseDueWork:SELECT", "CaseDueWork:UPDATE",
                "CaseHistory:INSERT", "CaseHistory:SELECT",
                "CaseIntakeLinks:INSERT", "CaseIntakeLinks:SELECT",
                "CaseMatchIndex:INSERT", "CaseMatchIndex:SELECT", "CaseMatchIndex:UPDATE",
                "CaseSequences:INSERT", "CaseSequences:SELECT", "CaseSequences:UPDATE",
                "CaseWorkflows:INSERT", "CaseWorkflows:SELECT", "CaseWorkflows:UPDATE",
                "Cases:INSERT", "Cases:SELECT", "Cases:UPDATE",
                "ExternalWorkItems:INSERT", "ExternalWorkItems:SELECT", "ExternalWorkItems:UPDATE",
                "IntakeMutationHistory:INSERT", "IntakeMutationHistory:SELECT",
                "OrganizationRoles:SELECT", "Organizations:SELECT",
                "PrincipalSequenceLineages:INSERT", "PrincipalSequenceLineages:SELECT",
                "Principals:SELECT", "Principals:UPDATE",
                "StandaloneAuditEvidence:INSERT", "StandaloneAuditEvidence:SELECT",
                "VehicleConfirmations:INSERT", "VehicleConfirmations:SELECT",
                "WorkflowConfigurations:SELECT"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    tableObject.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.objects AS tableObject
                    ON tableObject.object_id = permission.major_id
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE tableObject.name IN (
                        N'StandaloneAuditEvidence', N'Cases', N'CaseSequences',
                        N'CaseMatchIndex', N'CaseIntakeLinks', N'CaseHistory',
                        N'CaseWorkflows', N'CaseDataSnapshots', N'CaseDataFields',
                        N'CaseDueWork', N'ExternalWorkItems', N'IntakeMutationHistory',
                        N'Principals', N'PrincipalSequenceLineages', N'Organizations',
                        N'OrganizationRoles', N'VehicleConfirmations', N'WorkflowConfigurations')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND permission.[state] = 'G'
                  AND principal.name = N'{WorkerRole}'
                ORDER BY
                    tableObject.name COLLATE DATABASE_DEFAULT,
                    permission.permission_name COLLATE DATABASE_DEFAULT
                """));
    }

    [Fact]
    public async Task LatestMigrationGrantsOnlyWebReadAccessToMigrationHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(
            [$"{WebRole}:G:SELECT"],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    principal.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE permission.major_id = OBJECT_ID(N'[dbo].[__EFMigrationsHistory]')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name IN (N'{WebRole}', N'{WorkerRole}')
                """));
    }

    [Fact]
    public async Task LatestMigrationGivesOnlyWebExactCategoryCataloguePermissions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(
            [
                $"{WebRole}:D:DELETE",
                $"{WebRole}:G:INSERT",
                $"{WebRole}:G:SELECT",
                $"{WebRole}:G:UPDATE"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    principal.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE permission.major_id = OBJECT_ID(N'[dbo].[ApprovedOutlookCategories]')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name IN (N'{WebRole}', N'{WorkerRole}')
                """));
    }

    [Fact]
    public async Task TerminalUpgradeReconcilesEveryRuntimeTableToTheExactCallerMatrix()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(
            $"""
            GRANT SELECT ON OBJECT::[dbo].[ApplicationInitializations] TO [{WebRole}];
            GRANT DELETE ON OBJECT::[dbo].[Cases] TO [{WorkerRole}];
            """);
        await context.Database.MigrateAsync(RuntimeRoleMigration);

        Assert.Equal(2, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM sys.database_principals
            WHERE name IN (N'{WebRole}', N'{WorkerRole}')
              AND [type] = 'R'
              AND is_fixed_role = 0
              AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo')
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM sys.database_role_members
            WHERE role_principal_id IN (
                    DATABASE_PRINCIPAL_ID(N'{WebRole}'),
                    DATABASE_PRINCIPAL_ID(N'{WorkerRole}'))
               OR member_principal_id IN (
                    DATABASE_PRINCIPAL_ID(N'{WebRole}'),
                    DATABASE_PRINCIPAL_ID(N'{WorkerRole}'))
            """));

        var expectedTables = ParseLines(ExpectedSchemaTableSpec);
        Assert.Equal(
            expectedTables,
            await ReadValuesAsync(
                database,
                """
                SELECT name
                FROM sys.tables
                WHERE is_ms_shipped = 0
                  AND name <> N'__EFMigrationsHistory'
                """));
        Assert.Equal(
            ParseGrantSpec(ExpectedWebGrantSpec),
            await ReadGrantedPermissionsAsync(database, WebRole));
        Assert.Equal(
            ParseGrantSpec(ExpectedWorkerGrantSpec),
            await ReadGrantedPermissionsAsync(database, WorkerRole));
        Assert.Equal(
            expectedTables
                .Except(ParseLines(ExpectedWebDeleteTableSpec), StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            await ReadDeniedDeleteTablesAsync(database, WebRole));
        Assert.Equal(
            expectedTables,
            await ReadDeniedDeleteTablesAsync(database, WorkerRole));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM sys.database_permissions AS permission
            LEFT JOIN sys.objects AS target
                ON target.object_id = permission.major_id
            WHERE permission.grantee_principal_id IN (
                    DATABASE_PRINCIPAL_ID(N'{WebRole}'),
                    DATABASE_PRINCIPAL_ID(N'{WorkerRole}'))
              AND (
                    permission.class <> 1
                 OR permission.minor_id <> 0
                 OR target.[type] <> 'U'
                 OR permission.permission_name NOT IN (N'SELECT', N'INSERT', N'UPDATE', N'DELETE')
                 OR permission.[state] NOT IN ('G', 'D')
                 OR (permission.[state] = 'D' AND permission.permission_name <> N'DELETE'))
            """));
    }

    [Fact]
    public async Task LatestMigrationGivesFoundationTablesTheirExactRuntimePermissions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

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
        Assert.Equal(
            ParseGrantSpec(FoundationWebGrantSpec),
            (await ReadGrantedPermissionsAsync(database, WebRole))
                .Where(value => tables.Any(table => value.StartsWith($"{table}:", StringComparison.Ordinal)))
                .ToArray());
        Assert.Equal(
            ParseGrantSpec(FoundationWorkerGrantSpec)
                .Append("IntakeAssets:UPDATE")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            (await ReadGrantedPermissionsAsync(database, WorkerRole))
                .Where(value => value == "IntakeAssets:UPDATE"
                    || tables.Any(table => value.StartsWith($"{table}:", StringComparison.Ordinal)))
                .ToArray());
        foreach (var role in new[] { WebRole, WorkerRole })
        {
            var expectedDeniedTables = role == WorkerRole
                ? tables.Where(table => table != "DocumentContentCacheEntries").ToArray()
                : tables;
            Assert.Equal(expectedDeniedTables, (await ReadDeniedDeleteTablesAsync(database, role))
                .Where(tables.Contains)
                .ToArray());
        }
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

    [Fact]
    public async Task EngineerNotesMigrationCreatesTheTableWithExactWebAppendPermissions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'EngineerNotes'"));
        Assert.Equal(
            [
                $"{WebRole}:G:INSERT",
                $"{WebRole}:G:SELECT"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    principal.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE permission.major_id = OBJECT_ID(N'[dbo].[EngineerNotes]')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name IN (N'{WebRole}', N'{WorkerRole}')
                """));
    }

    [Fact]
    public async Task RetainedMailSearchProjectionUsesExactCallerPermissions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(
            ["IntakeSearchDocuments:SELECT"],
            (await ReadGrantedPermissionsAsync(database, WebRole))
                .Where(value => value.StartsWith("IntakeSearchDocuments:", StringComparison.Ordinal))
                .ToArray());
        Assert.Equal(
            [
                "IntakeSearchDocuments:DELETE",
                "IntakeSearchDocuments:INSERT",
                "IntakeSearchDocuments:SELECT"
            ],
            (await ReadGrantedPermissionsAsync(database, WorkerRole))
                .Where(value => value.StartsWith("IntakeSearchDocuments:", StringComparison.Ordinal))
                .ToArray());
    }

    [Fact]
    public async Task RetainedMailFolderMovesUseExactWebOnlyAppendPermissions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(
            [
                "RetainedMailFolderMoves:INSERT",
                "RetainedMailFolderMoves:SELECT",
                "RetainedMailFolderMoves:UPDATE"
            ],
            (await ReadGrantedPermissionsAsync(database, WebRole))
                .Where(value => value.StartsWith("RetainedMailFolderMoves:", StringComparison.Ordinal))
                .ToArray());
        Assert.DoesNotContain(
            await ReadGrantedPermissionsAsync(database, WorkerRole),
            value => value.StartsWith("RetainedMailFolderMoves:", StringComparison.Ordinal));
        Assert.Contains("RetainedMailFolderMoves", await ReadDeniedDeleteTablesAsync(database, WebRole));
        Assert.Contains("RetainedMailFolderMoves", await ReadDeniedDeleteTablesAsync(database, WorkerRole));
    }

    [Fact]
    public async Task LatestMigrationGrantsWorkerAutomaticVehicleLookupInsert()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(
            [
                "VehicleLookupRequests:INSERT",
                "VehicleLookupRequests:SELECT"
            ],
            (await ReadGrantedPermissionsAsync(database, WorkerRole))
                .Where(value => value.StartsWith("VehicleLookupRequests:", StringComparison.Ordinal))
                .ToArray());
        Assert.Contains("VehicleLookupRequests", await ReadDeniedDeleteTablesAsync(database, WorkerRole));
    }

    // DOCS-008: DOCS-007 moved case-document registration into the Worker's
    // custody processor while these three tables were granted to Web only, so
    // every deployed case uploaded its evidence to Box and was then refused the
    // record write. Nothing here caught it because the tests run
    // full-privilege; this asserts the grant itself.
    [Fact]
    public async Task LatestMigrationGrantsWorkerTheCaseDocumentTables()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        var granted = await ReadGrantedPermissionsAsync(database, WorkerRole);
        var deniedDelete = await ReadDeniedDeleteTablesAsync(database, WorkerRole);
        foreach (var (table, expected) in new[]
        {
            ("CaseDocuments", new[] { "CaseDocuments:INSERT", "CaseDocuments:SELECT" }),
            ("DocumentOccurrences", ["DocumentOccurrences:INSERT", "DocumentOccurrences:SELECT"]),
            ("DocumentVersions",
                ["DocumentVersions:INSERT", "DocumentVersions:SELECT", "DocumentVersions:UPDATE"])
        })
        {
            Assert.Equal(
                expected,
                granted
                    .Where(value => value.StartsWith($"{table}:", StringComparison.Ordinal))
                    .ToArray());
            Assert.Contains(table, deniedDelete);
        }
    }

    [Fact]
    public async Task LatestMigrationGrantsImageIntakeLifecycleUpdatesToBothRuntimeRoles()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        foreach (var role in new[] { WebRole, WorkerRole })
        {
            Assert.Equal(
                [
                    "ImageIntakes:INSERT",
                    "ImageIntakes:SELECT",
                    "ImageIntakes:UPDATE"
                ],
                (await ReadGrantedPermissionsAsync(database, role))
                    .Where(value => value.StartsWith("ImageIntakes:", StringComparison.Ordinal))
                    .ToArray());
            Assert.Contains("ImageIntakes", await ReadDeniedDeleteTablesAsync(database, role));
        }
    }

    [Fact]
    public async Task LatestMigrationGivesBothRuntimeRolesAppendOnlyImageLifecycleEvents()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync();

        Assert.Equal(
            [
                $"{WebRole}:D:DELETE",
                $"{WebRole}:D:UPDATE",
                $"{WebRole}:G:INSERT",
                $"{WebRole}:G:SELECT",
                $"{WorkerRole}:D:DELETE",
                $"{WorkerRole}:D:UPDATE",
                $"{WorkerRole}:G:INSERT",
                $"{WorkerRole}:G:SELECT"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    principal.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions AS permission
                INNER JOIN sys.database_principals AS principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE permission.major_id = OBJECT_ID(N'[dbo].[ImageIntakeLifecycleEvents]')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name IN (N'{WebRole}', N'{WorkerRole}')
                ORDER BY
                    principal.name COLLATE DATABASE_DEFAULT,
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    permission.permission_name COLLATE DATABASE_DEFAULT
                """));
    }

    [Fact]
    public async Task LatestMigrationGrantsOnlyTheRequiredSubscriptionPermissions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

        Assert.Equal(
            [
                $"{WebRole}:G:SELECT",
                $"{WorkerRole}:G:INSERT",
                $"{WorkerRole}:G:SELECT",
                $"{WorkerRole}:G:UPDATE"
            ],
            await ReadValuesAsync(
                database,
                $"""
                SELECT CONCAT(
                    principal.name COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.[state] COLLATE DATABASE_DEFAULT,
                    N':',
                    permission.permission_name COLLATE DATABASE_DEFAULT)
                FROM sys.database_permissions permission
                INNER JOIN sys.database_principals principal
                    ON principal.principal_id = permission.grantee_principal_id
                WHERE permission.major_id = OBJECT_ID(N'[dbo].[ApprovedMailboxSubscriptions]')
                  AND permission.class = 1
                  AND permission.minor_id = 0
                  AND principal.name IN (N'{WebRole}', N'{WorkerRole}')
                """));
    }

    // PLAT-035: catalog-grant checks alone run as the LocalDB administrator and
    // therefore cannot detect a real runtime save denied by SQL Server. These
    // loginless users have only their corresponding Pegasus runtime role.
    [Fact]
    public async Task V1FoundationRuntimeRolesCanPerformOnlyTheirRepresentativeWrites()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

        await database.ExecuteAsync(
            $"""
            CREATE USER [pegasus_test_web_runtime] WITHOUT LOGIN;
            CREATE USER [pegasus_test_worker_runtime] WITHOUT LOGIN;
            ALTER ROLE [{WebRole}] ADD MEMBER [pegasus_test_web_runtime];
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_test_worker_runtime];
            """);

        var claimSourceId = Guid.NewGuid();
        var webConcurrencyToken = Guid.NewGuid();

        await database.ExecuteAsync(
            $"""
            EXECUTE AS USER = N'pegasus_test_web_runtime';
            INSERT INTO [dbo].[ClaimSources] (
                [Id], [Name], [Active], [UpdatedBy], [UpdatedAtUtc], [Version], [ConcurrencyToken])
            VALUES (
                '{claimSourceId:D}', N'restricted-role-fixture', 1, N'test',
                '2031-05-06T10:30:00+00:00', 0, '{webConcurrencyToken:D}');
            UPDATE [dbo].[PublicUploadOccurrences]
            SET [CustodyState] = [CustodyState]
            WHERE [Id] = '00000000-0000-0000-0000-000000000000';
            REVERT;

            EXECUTE AS USER = N'pegasus_test_worker_runtime';
            UPDATE [dbo].[TriageSequences]
            SET [LastAllocatedSequence] = 1
            WHERE [Id] = 1;
            DELETE FROM [dbo].[DocumentContentCacheEntries]
            WHERE [Id] = '00000000-0000-0000-0000-000000000000';
            REVERT;
            """);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [dbo].[ClaimSources] WHERE [Id] = '{claimSourceId:D}'"));
        Assert.Equal(1L, await database.ScalarAsync<long>(
            "SELECT [LastAllocatedSequence] FROM [dbo].[TriageSequences] WHERE [Id] = 1"));

        await database.ExecuteAsync(
            $"""
            EXECUTE AS USER = N'pegasus_test_web_runtime';
            BEGIN TRY
                DELETE FROM [dbo].[ClaimSources] WHERE [Id] = '{claimSourceId:D}';
                THROW 51000, 'Web runtime unexpectedly deleted a ClaimSource.', 1;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() <> 229 THROW;
            END CATCH;
            REVERT;

            EXECUTE AS USER = N'pegasus_test_worker_runtime';
            BEGIN TRY
                INSERT INTO [dbo].[ClaimSources] (
                    [Id], [Name], [Active], [UpdatedBy], [UpdatedAtUtc], [Version], [ConcurrencyToken])
                VALUES (
                    '{Guid.NewGuid():D}', N'forbidden', 1, N'test',
                    '2031-05-06T10:30:00+00:00', 0, '{Guid.NewGuid():D}');
                THROW 51000, 'Worker runtime unexpectedly inserted a ClaimSource.', 1;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() <> 229 THROW;
            END CATCH;
            REVERT;

            EXECUTE AS USER = N'pegasus_test_worker_runtime';
            BEGIN TRY
                UPDATE [dbo].[PublicUploadOccurrences]
                SET [CustodyState] = [CustodyState]
                WHERE [Id] = '00000000-0000-0000-0000-000000000000';
                THROW 51000, 'Worker runtime unexpectedly updated a PublicUploadOccurrence.', 1;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() <> 229 THROW;
            END CATCH;
            REVERT;
            """);
    }

    [Fact]
    public async Task WebRuntimeCanInsertPairedOcrWorkRowsButCannotProcessOcr()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

        var receiptId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        context.Add(new IntakeReceiptEntity
        {
            Id = receiptId,
            SourceFileName = "ocr-permission.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('A', 64),
            SourceChannel = "manual_upload",
            ExternalReceiptToken = $"ocr-permission:{receiptId:N}",
            ReceivedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
            ProcessedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 31, 0, TimeSpan.Zero),
            SourceReaderKey = "runtime-role-test",
            SourceReaderVersion = "1",
            Version = 0,
            Decision = "retained",
            DecisionReason = "runtime role permission fixture",
            EvidenceJson = "[]",
            FieldsJson = "{}",
            OcrCandidatesJson = "[]"
        });
        context.Add(new IntakeAssetEntity
        {
            Id = assetId,
            IntakeReceiptId = receiptId,
            SourceLabel = "attachment",
            FileName = "evidence.pdf",
            MediaType = "application/pdf",
            Kind = "attachment",
            Disposition = "retained",
            ContentLength = 1,
            ContentHash = new string('B', 64),
            StorageKey = $"runtime-role/{assetId:N}"
        });
        await context.SaveChangesAsync();

        var operationId = Guid.NewGuid();
        var workId = Guid.NewGuid();
        await database.ExecuteAsync(
            $"""
            CREATE USER [pegasus_test_ocr_web] WITHOUT LOGIN;
            ALTER ROLE [{WebRole}] ADD MEMBER [pegasus_test_ocr_web];
            EXECUTE AS USER = N'pegasus_test_ocr_web';
            BEGIN TRANSACTION;
            INSERT INTO [dbo].[IntakeOcrOperations] (
                [Id], [IntakeAssetId], [SourceSha256], [QualifiedPagesJson],
                [OperationKey], [State], [Version], [ConcurrencyToken])
            VALUES (
                '{operationId:D}', '{assetId:D}', REPLICATE(N'B', 64), N'[1]',
                N'ocr-permission-operation', N'Pending', 0, '{Guid.NewGuid():D}');
            INSERT INTO [dbo].[ExternalWorkItems] (
                [Id], [Kind], [OperationKey], [State], [AttemptCount], [DueAtUtc])
            VALUES (
                '{workId:D}', N'intake_ocr', N'ocr-permission-operation', N'pending', 0,
                '2031-05-06T10:32:00+00:00');
            COMMIT TRANSACTION;
            BEGIN TRY
                UPDATE [dbo].[IntakeOcrOperations]
                SET [State] = N'Completed'
                WHERE [Id] = '{operationId:D}';
                THROW 51000, 'Web runtime unexpectedly processed OCR work.', 1;
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() <> 229 THROW;
            END CATCH;
            REVERT;
            """);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [dbo].[IntakeOcrOperations] WHERE [Id] = '{operationId:D}' AND [State] = N'Pending'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM [dbo].[ExternalWorkItems] WHERE [Id] = '{workId:D}' AND [State] = N'pending'"));
    }

    [Fact]
    public async Task RetainedMailboxReplyTargetsRoundTripThroughRestrictedRuntimeRoles()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

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

        var withReplyToId = Guid.NewGuid();
        var withoutReplyToId = Guid.NewGuid();
        await database.ExecuteAsync(
            $"""
            INSERT INTO [dbo].[ApprovedInboxPollStates] (
                [ApprovedMailboxId], [MailboxAddress], [ScopeFingerprint], [Generation],
                [ActivatedAtUtc], [StartBoundaryUtc], [DueAtUtc])
            VALUES (
                '49f47eb9-c5b0-464f-b8f0-8c90ba061728',
                N'instructions@collisionengineers.co.uk', REPLICATE(N'C', 64), 1,
                '2031-05-06T10:00:00+00:00', '2031-05-06T10:00:00+00:00',
                '2031-05-06T10:00:00+00:00');

            CREATE USER [pegasus_test_reply_web] WITHOUT LOGIN;
            CREATE USER [pegasus_test_reply_worker] WITHOUT LOGIN;
            ALTER ROLE [{WebRole}] ADD MEMBER [pegasus_test_reply_web];
            ALTER ROLE [{WorkerRole}] ADD MEMBER [pegasus_test_reply_worker];

            DECLARE @MailboxId uniqueidentifier =
                (SELECT TOP (1) [ApprovedMailboxId] FROM [dbo].[ApprovedInboxPollStates]);
            IF @MailboxId IS NULL
                THROW 51000, 'The retained-mailbox role fixture requires an approved mailbox.', 1;

            EXECUTE AS USER = N'pegasus_test_reply_worker';
            INSERT INTO [dbo].[RetainedMailboxMessages] (
                [Id], [MailboxId], [MailboxAddress], [FolderScope], [FolderIdentity],
                [ImmutableMessageId], [ExternalReceiptToken], [ToAddressesJson],
                [CcAddressesJson], [ReplyToAddressesJson], [IsRead], [SourceLength],
                [SourceSha256], [ReceivedAtUtc], [RetainedAtUtc])
            VALUES
                ('{withReplyToId:D}', @MailboxId, N'intake@example.test', N'Inbox', N'inbox',
                 N'reply-target-message', N'reply-target-receipt', N'[]', N'[]',
                 N'["reply@example.test"]', 0, 1, REPLICATE(N'A', 64),
                 '2031-05-06T10:30:00+00:00', '2031-05-06T10:31:00+00:00'),
                ('{withoutReplyToId:D}', @MailboxId, N'intake@example.test', N'Inbox', N'inbox',
                 N'no-reply-target-message', N'no-reply-target-receipt', N'[]', N'[]',
                 NULL, 0, 1, REPLICATE(N'B', 64),
                 '2031-05-06T10:32:00+00:00', '2031-05-06T10:33:00+00:00');
            REVERT;

            EXECUTE AS USER = N'pegasus_test_reply_web';
            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[RetainedMailboxMessages]
                WHERE [Id] = '{withReplyToId:D}'
                  AND [ReplyToAddressesJson] = N'["reply@example.test"]')
                THROW 51000, 'Web runtime did not read the retained reply targets.', 1;
            IF NOT EXISTS (
                SELECT 1 FROM [dbo].[RetainedMailboxMessages]
                WHERE [Id] = '{withoutReplyToId:D}'
                  AND [ReplyToAddressesJson] IS NULL)
                THROW 51000, 'Web runtime did not preserve absent reply metadata as NULL.', 1;
            REVERT;
            """);
    }

    [Fact]
    public async Task LatestSchemaRetainsOneLabourRateAndRemovesTheObsoletePaintRate()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            migrate: false,
            useTemplate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

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
    public async Task TerminalDowngradeRestoresTheExactPreTerminalPermissionState()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        var before = await ReadPermissionSnapshotAsync(database);

        await context.Database.MigrateAsync(RuntimeRoleMigration);
        await context.Database.MigrateAsync(PreviousMigration);

        Assert.Equal(before, await ReadPermissionSnapshotAsync(database));
    }

    [Fact]
    public async Task V1FoundationCanDowngradeAndReapplyItsCurrentSchema()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(useTemplate: false);
        await using var context = await database.CreateContextAsync();
        Assert.False(context.Database.HasPendingModelChanges());

        await context.Database.MigrateAsync("20260905010654_CaseSignOffEngineer");
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN ('ValuationPresets', 'AppliedValuationSnapshots')"));

        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.Equal(2, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_ValuationPresets_Label', 'IX_AppliedValuationSnapshots_CaseId_AcceptedAtUtc')"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = 'IX_ValuationPresets_Label' AND is_unique = 1 AND has_filter = 0"));
    }

    [Fact]
    public async Task OriginalRoleMigrationDowngradeRemovesOnlyItsManagedRoles()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(OriginalRuntimeRoleMigration);
        await context.Database.MigrateAsync(PreRuntimeRoleMigration);

        Assert.Equal(0, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM sys.database_principals
            WHERE name IN (N'{WebRole}', N'{WorkerRole}')
            """));
    }

    private static string[] ParseLines(string spec) =>
        spec.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] ParseGrantSpec(string spec) =>
        ParseLines(spec)
            .SelectMany(line =>
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                var table = line[..separator];
                return line[(separator + 1)..]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(permission => $"{table}:{permission}");
            })
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static Task<string[]> ReadGrantedPermissionsAsync(
        LocalDbTestDatabase database,
        string role) =>
        ReadValuesAsync(
            database,
            $"""
            SELECT CONCAT(
                target.name COLLATE DATABASE_DEFAULT,
                N':',
                permission.permission_name COLLATE DATABASE_DEFAULT)
            FROM sys.database_permissions AS permission
            INNER JOIN sys.objects AS target
                ON target.object_id = permission.major_id
            WHERE permission.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'{role}')
              AND permission.class = 1
              AND permission.minor_id = 0
              AND permission.[state] = 'G'
            """);

    private static Task<string[]> ReadDeniedDeleteTablesAsync(
        LocalDbTestDatabase database,
        string role) =>
        ReadValuesAsync(
            database,
            $"""
            SELECT target.name
            FROM sys.database_permissions AS permission
            INNER JOIN sys.objects AS target
                ON target.object_id = permission.major_id
            WHERE permission.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'{role}')
              AND permission.class = 1
              AND permission.minor_id = 0
              AND permission.permission_name = N'DELETE'
              AND permission.[state] = 'D'
            """);

    private static Task<string[]> ReadPermissionSnapshotAsync(
        LocalDbTestDatabase database) =>
        ReadValuesAsync(
            database,
            $"""
            SELECT CONCAT(
                principal.name COLLATE DATABASE_DEFAULT,
                N':',
                target.name COLLATE DATABASE_DEFAULT,
                N':',
                permission.permission_name COLLATE DATABASE_DEFAULT,
                N':',
                permission.[state] COLLATE DATABASE_DEFAULT)
            FROM sys.database_permissions AS permission
            INNER JOIN sys.database_principals AS principal
                ON principal.principal_id = permission.grantee_principal_id
            INNER JOIN sys.objects AS target
                ON target.object_id = permission.major_id
            WHERE principal.name IN (N'{WebRole}', N'{WorkerRole}')
            """);

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
