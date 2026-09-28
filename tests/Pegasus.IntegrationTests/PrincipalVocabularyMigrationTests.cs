using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The work-provider "Provider" vocabulary became "Principal" (#857). The
/// migration renames the tables, columns, constraints and indexes and rewrites
/// every persisted code the renamed source writes. These tests run it over a
/// database that still holds the old spellings, so a row written before the
/// rename is read under the new vocabulary afterwards.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PrincipalVocabularyMigrationTests
{
    private const string PreviousMigration = "20260929093000_MarketResearchAttachedEvent";
    private const string VocabularyMigration = "20260929120000_PrincipalVocabulary";
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    [Fact]
    public async Task TablesColumnsConstraintsAndIndexesTakeThePrincipalNames()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);

        foreach (var table in OldTables)
        {
            Assert.Equal(1, await TableCountAsync(database, table));
        }
        Assert.Equal(1, await ColumnCountAsync(database, "ProviderSubmissions", "ProviderReference"));
        Assert.Equal(1, await ColumnCountAsync(database, "CaseMatchIndex", "WorkProviderCode"));
        Assert.Equal(1, await ColumnCountAsync(database, "IntakeMailRouteDecisions", "WorkProviderCode"));

        await context.Database.MigrateAsync();

        Assert.Contains(VocabularyMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        foreach (var table in OldTables)
        {
            Assert.Equal(0, await TableCountAsync(database, table));
        }
        foreach (var table in NewTables)
        {
            Assert.Equal(1, await TableCountAsync(database, table));
        }

        Assert.Equal(0, await ColumnCountAsync(database, "PrincipalSubmissions", "ProviderReference"));
        Assert.Equal(1, await ColumnCountAsync(database, "PrincipalSubmissions", "PrincipalReference"));
        Assert.Equal(0, await ColumnCountAsync(database, "CaseMatchIndex", "WorkProviderCode"));
        Assert.Equal(1, await ColumnCountAsync(database, "CaseMatchIndex", "PrincipalCode"));
        Assert.Equal(0, await ColumnCountAsync(database, "IntakeMailRouteDecisions", "WorkProviderCode"));
        Assert.Equal(1, await ColumnCountAsync(database, "IntakeMailRouteDecisions", "PrincipalCode"));

        foreach (var constraint in new[]
        {
            "CK_PrincipalDomainPackages_SchemaVersion",
            "CK_PrincipalDomainPackages_SourceRowCount",
            "CK_PrincipalReferences_SourceRow"
        })
        {
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'{constraint}'"));
        }
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name LIKE N'%ProviderDomain%' OR name LIKE N'%ProviderReference%' OR name LIKE N'%ProviderSubmission%'"));

        foreach (var (table, index) in new[]
        {
            ("PrincipalSubmissions", "IX_PrincipalSubmissions_PrincipalId_IdempotencyKey"),
            ("PrincipalDomainEvidence", "IX_PrincipalDomainEvidence_Version_DomainSuffix"),
            ("CaseMatchIndex", "IX_CaseMatchIndex_PrincipalCode_DurableClaimToken"),
            ("CaseMatchIndex", "IX_CaseMatchIndex_PrincipalCode_NormalizedSurname"),
            ("CaseMatchIndex", "IX_CaseMatchIndex_PrincipalCode_NormalizedVrm")
        })
        {
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'{index}'"));
        }
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.indexes WHERE name LIKE N'%ProviderDomain%' OR name LIKE N'%ProviderReference%' OR name LIKE N'%ProviderSubmission%' OR name LIKE N'%WorkProviderCode%'"));
    }

    [Fact]
    public async Task TheSeededReferencePackageSurvivesTheRenameUnchanged()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        var packageHash = await database.ScalarAsync<string>(
            "SELECT PackageSha256 FROM ProviderDomainPackages WHERE Version = 'provider-domains-v1'");

        await context.Database.MigrateAsync();

        // The package identity is hash-pinned, so the migration keeps it as is.
        Assert.Equal(
            packageHash,
            await database.ScalarAsync<string>(
                "SELECT PackageSha256 FROM PrincipalDomainPackages WHERE Version = 'provider-domains-v1'"));
        Assert.Equal(1, await database.ScalarAsync<int>("SELECT COUNT(*) FROM PrincipalDomainPackages"));
        Assert.Equal(11, await database.ScalarAsync<int>("SELECT COUNT(*) FROM PrincipalReferences"));
        Assert.Equal(16, await database.ScalarAsync<int>("SELECT COUNT(*) FROM PrincipalDomainEvidence"));
    }

    [Fact]
    public async Task StoredCodesAreRewrittenAndUnrelatedRowsAreKept()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(OldVocabularyRowsSql);

        await context.Database.MigrateAsync();

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM OrganizationRoles WHERE OrganizationId = '87000000-0000-0000-0000-000000000001' AND Role = N'principal'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM OrganizationRoles WHERE Role = N'work_provider'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM OrganizationRoles WHERE OrganizationId = '87000000-0000-0000-0000-000000000002' AND Role = N'instruction_intermediary'"));

        foreach (var (id, reason) in new[]
        {
            ("87000000-0000-0000-0000-000000000011", "principal_credential_rejected"),
            ("87000000-0000-0000-0000-000000000012", "principal_credential_missing"),
            ("87000000-0000-0000-0000-000000000013", "principal_credential_paused"),
            ("87000000-0000-0000-0000-000000000014", "principal_mismatch"),
            ("87000000-0000-0000-0000-000000000015", "principal_api_rate_limited")
        })
        {
            Assert.Equal(reason, await database.ScalarAsync<string>(
                $"SELECT ReasonCode FROM SecurityEvents WHERE Id = '{id}'"));
        }
        Assert.Equal("Principal", await database.ScalarAsync<string>(
            "SELECT ActorKind FROM SecurityEvents WHERE Id = '87000000-0000-0000-0000-000000000011'"));

        // A code the migration does not own is left exactly as it was.
        Assert.Equal("staff_login_failed", await database.ScalarAsync<string>(
            "SELECT ReasonCode FROM SecurityEvents WHERE Id = '87000000-0000-0000-0000-000000000016'"));
        Assert.Equal("Staff", await database.ScalarAsync<string>(
            "SELECT ActorKind FROM SecurityEvents WHERE Id = '87000000-0000-0000-0000-000000000016'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM SecurityEvents WHERE ReasonCode LIKE N'provider[_]%' OR ActorKind = N'Provider'"));
    }

    [Fact]
    public async Task ActionHistoryCodesAndSubmissionKeysAreRewritten()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(
            """
            INSERT INTO ActionHistory
                (Id, ActorKind, ActorRolesJson, ActorSubjectId, AggregateId, AggregateType, CorrelationId,
                 EventKind, OccurredAtUtc, Outcome, BeforeJson, AfterJson)
            VALUES
                ('87000000-0000-0000-0000-000000000031', N'Provider', N'[]', N'principal-1',
                 N'87000000-0000-0000-0000-0000000000a1', N'ProviderSubmission',
                 N'provider-submission:87000000000000000000000000000a1', N'Submitted',
                 '2031-07-01T09:00:00+00:00', N'Accepted',
                 N'{"State":"ProviderCancelled"}', N'{"ClosureOutcome":"ProviderCancelled"}'),
                ('87000000-0000-0000-0000-000000000032', N'Staff', N'[]', N'staff-1',
                 N'87000000-0000-0000-0000-0000000000a2', N'CaseWorkflow',
                 N'corr-staff', N'Reopened',
                 '2031-07-01T09:01:00+00:00', N'Accepted', NULL, NULL);
            """);

        await context.Database.MigrateAsync();

        const string migrated = "87000000-0000-0000-0000-000000000031";
        Assert.Equal("Principal", await database.ScalarAsync<string>(
            $"SELECT ActorKind FROM ActionHistory WHERE Id = '{migrated}'"));
        Assert.Equal("PrincipalSubmission", await database.ScalarAsync<string>(
            $"SELECT AggregateType FROM ActionHistory WHERE Id = '{migrated}'"));
        Assert.Equal("principal-submission:87000000000000000000000000000a1", await database.ScalarAsync<string>(
            $"SELECT CorrelationId FROM ActionHistory WHERE Id = '{migrated}'"));
        Assert.Equal("{\"State\":\"PrincipalCancelled\"}", await database.ScalarAsync<string>(
            $"SELECT BeforeJson FROM ActionHistory WHERE Id = '{migrated}'"));
        Assert.Equal("{\"ClosureOutcome\":\"PrincipalCancelled\"}", await database.ScalarAsync<string>(
            $"SELECT AfterJson FROM ActionHistory WHERE Id = '{migrated}'"));

        // A row the migration does not own keeps its actor and aggregate.
        Assert.Equal("Staff", await database.ScalarAsync<string>(
            "SELECT ActorKind FROM ActionHistory WHERE Id = '87000000-0000-0000-0000-000000000032'"));
        Assert.Equal("CaseWorkflow", await database.ScalarAsync<string>(
            "SELECT AggregateType FROM ActionHistory WHERE Id = '87000000-0000-0000-0000-000000000032'"));
    }

    [Fact]
    public async Task TheCodeCheckConstraintsAcceptThePrincipalCodesAndRefuseTheOldOnes()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_OrganizationRoles_Role' AND definition LIKE N'%''principal''%' AND definition NOT LIKE N'%work_provider%'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_CaseDataFields_SourceKind' AND definition LIKE N'%principal_setting%' AND definition LIKE N'%principal_api%' AND definition NOT LIKE N'%provider_%'"));

        await database.ExecuteAsync(
            """
            INSERT INTO Organizations (Id, Name, Version)
            VALUES ('87000000-0000-0000-0000-000000000021', 'Principal role organization', 0);
            INSERT INTO OrganizationRoles (OrganizationId, Role)
            VALUES ('87000000-0000-0000-0000-000000000021', N'principal');
            """);
        await Assert.ThrowsAnyAsync<Exception>(() => database.ExecuteAsync(
            "INSERT INTO OrganizationRoles (OrganizationId, Role) VALUES ('87000000-0000-0000-0000-000000000021', N'work_provider')"));
    }

    [Fact]
    public async Task RuntimeRolesHoldTheirGrantsOnTheRenamedTables()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync();

        foreach (var permission in new[] { "SELECT", "INSERT", "UPDATE" })
        {
            Assert.Equal(1, await GrantCountAsync(database, WebRole, "PrincipalSubmissions", permission));
        }
        Assert.Equal(0, await GrantCountAsync(database, WebRole, "PrincipalSubmissions", "DELETE"));
        Assert.Equal(1, await GrantCountAsync(database, WorkerRole, "PrincipalSubmissions", "SELECT"));
        Assert.Equal(1, await GrantCountAsync(database, WorkerRole, "PrincipalSubmissions", "UPDATE"));
        Assert.Equal(0, await GrantCountAsync(database, WorkerRole, "PrincipalSubmissions", "INSERT"));
        Assert.Equal(0, await GrantCountAsync(database, WorkerRole, "PrincipalSubmissions", "DELETE"));

        foreach (var table in new[] { "PrincipalDomainEvidence", "PrincipalDomainPackages", "PrincipalReferences" })
        {
            foreach (var role in new[] { WebRole, WorkerRole })
            {
                Assert.Equal(1, await GrantCountAsync(database, role, table, "SELECT"));
                foreach (var permission in new[] { "INSERT", "UPDATE", "DELETE" })
                {
                    Assert.Equal(0, await GrantCountAsync(database, role, table, permission));
                }
            }
        }
    }

    private static readonly string[] OldTables =
    [
        "ProviderSubmissions", "ProviderDomainPackages", "ProviderReferences", "ProviderDomainEvidence"
    ];

    private static readonly string[] NewTables =
    [
        "PrincipalSubmissions", "PrincipalDomainPackages", "PrincipalReferences", "PrincipalDomainEvidence"
    ];

    private static Task<int> TableCountAsync(LocalDbTestDatabase database, string table) =>
        database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM sys.tables WHERE name = N'{table}' AND schema_id = SCHEMA_ID(N'dbo')");

    private static Task<int> ColumnCountAsync(LocalDbTestDatabase database, string table, string column) =>
        database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'{column}'");

    private static Task<int> GrantCountAsync(
        LocalDbTestDatabase database,
        string role,
        string table,
        string permission) =>
        database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*)
            FROM sys.database_permissions AS grants
            INNER JOIN sys.objects AS target ON target.object_id = grants.major_id
            WHERE grants.class = 1
              AND grants.minor_id = 0
              AND grants.[state] = 'G'
              AND grants.permission_name = N'{permission}'
              AND grants.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'{role}')
              AND target.name = N'{table}'
            """);

    private const string OldVocabularyRowsSql =
        """
        INSERT INTO Organizations (Id, Name, Version)
        VALUES
            ('87000000-0000-0000-0000-000000000001', 'Old work provider organization', 0),
            ('87000000-0000-0000-0000-000000000002', 'Intermediary organization', 0);

        INSERT INTO OrganizationRoles (OrganizationId, Role)
        VALUES
            ('87000000-0000-0000-0000-000000000001', N'work_provider'),
            ('87000000-0000-0000-0000-000000000002', N'instruction_intermediary');

        INSERT INTO SecurityEvents
            (Id, Type, Outcome, SubjectId, CorrelationId, ReasonCode, ActorKind, ActorSubjectId, OccurredAtUtc)
        VALUES
            ('87000000-0000-0000-0000-000000000011', N'Token', N'Denied', N'subject-1', N'corr-1',
             N'provider_credential_rejected', N'Provider', N'principal-1', '2031-07-01T09:00:00+00:00'),
            ('87000000-0000-0000-0000-000000000012', N'Token', N'Denied', N'subject-2', N'corr-2',
             N'provider_credential_missing', NULL, NULL, '2031-07-01T09:01:00+00:00'),
            ('87000000-0000-0000-0000-000000000013', N'Token', N'Denied', N'subject-3', N'corr-3',
             N'provider_credential_paused', NULL, NULL, '2031-07-01T09:02:00+00:00'),
            ('87000000-0000-0000-0000-000000000014', N'Token', N'Denied', N'subject-4', N'corr-4',
             N'provider_principal_mismatch', NULL, NULL, '2031-07-01T09:03:00+00:00'),
            ('87000000-0000-0000-0000-000000000015', N'Token', N'Denied', N'subject-5', N'corr-5',
             N'provider_api_rate_limited', NULL, NULL, '2031-07-01T09:04:00+00:00'),
            ('87000000-0000-0000-0000-000000000016', N'SignIn', N'Denied', N'subject-6', N'corr-6',
             N'staff_login_failed', N'Staff', N'staff-1', '2031-07-01T09:05:00+00:00');
        """;
}
