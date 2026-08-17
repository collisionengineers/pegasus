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
