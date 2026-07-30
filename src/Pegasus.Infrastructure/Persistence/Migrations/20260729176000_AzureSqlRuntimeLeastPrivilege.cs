using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729176000_AzureSqlRuntimeLeastPrivilege")]
public sealed class AzureSqlRuntimeLeastPrivilege : Migration
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
        {
            return;
        }

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                THROW 51000, N'The reserved Pegasus Web runtime role already exists.', 1;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                THROW 51000, N'The reserved Pegasus Worker runtime role already exists.', 1;

            CREATE ROLE [pegasus_web_runtime_role] AUTHORIZATION [dbo];
            CREATE ROLE [pegasus_worker_runtime_role] AUTHORIZATION [dbo];
            """);

        // ASP.NET Core Identity and OpenIddict are owned exclusively by the Web process.
        Grant(migrationBuilder, WebRole, "AspNetRoles", "SELECT");
        Grant(migrationBuilder, WebRole, "AspNetRoleClaims", "SELECT");
        Grant(migrationBuilder, WebRole, "AspNetUsers", "SELECT, INSERT, UPDATE, DELETE");
        Grant(migrationBuilder, WebRole, "AspNetUserClaims", "SELECT, INSERT, UPDATE, DELETE");
        Grant(migrationBuilder, WebRole, "AspNetUserLogins", "SELECT, INSERT, DELETE");
        Grant(migrationBuilder, WebRole, "AspNetUserRoles", "SELECT, INSERT, DELETE");
        Grant(migrationBuilder, WebRole, "AspNetUserTokens", "SELECT, INSERT, UPDATE, DELETE");
        Grant(migrationBuilder, WebRole, "OpenIddictApplications", "SELECT");
        Grant(migrationBuilder, WebRole, "OpenIddictAuthorizations", "SELECT, INSERT, UPDATE, DELETE");
        Grant(migrationBuilder, WebRole, "OpenIddictScopes", "SELECT");
        Grant(migrationBuilder, WebRole, "OpenIddictTokens", "SELECT, INSERT, UPDATE, DELETE");
        Grant(migrationBuilder, WebRole, "SecurityEvents", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "ActionHistory", "SELECT, INSERT");

        // Staff-facing intake, Triage, case allocation, and case-workflow callers.
        Grant(migrationBuilder, WebRole, "Organizations", "SELECT");
        Grant(migrationBuilder, WebRole, "OrganizationRoles", "SELECT");
        Grant(migrationBuilder, WebRole, "Principals", "SELECT");
        Grant(migrationBuilder, WebRole, "PrincipalSequenceLineages", "SELECT");
        Grant(migrationBuilder, WebRole, "CaseSequences", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WebRole, "Cases", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WebRole, "CaseHistory", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "CaseIntakeLinks", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "CaseWorkflows", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WebRole, "CaseWorkflowEvents", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "CaseDueWork", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WebRole, "CaseEngineerFindings", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "CaseManualChases", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "CaseReportApprovals", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "CaseReportSentEvidence", "SELECT, UPDATE");
        Grant(migrationBuilder, WebRole, "ExternalWorkItems", "INSERT");

        // Case-document custody and public request-upload callers execute in Web.
        Grant(migrationBuilder, WebRole, "CaseDocuments", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "DocumentVersions", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WebRole, "DocumentOccurrences", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "RequestUploadLinks", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WebRole, "RequestUploadReceipts", "SELECT, INSERT");

        // Production intake upload stages durable work in Web; dispatch and processing remain Worker-owned.
        Grant(migrationBuilder, WebRole, "IntakeStagedReceipts", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "IntakeWorkItems", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "IntakeReceipts", "SELECT, UPDATE");
        Grant(migrationBuilder, WebRole, "IntakeAssets", "SELECT");
        Grant(migrationBuilder, WebRole, "IntakeMailRouteDecisions", "SELECT");
        Grant(migrationBuilder, WebRole, "IntakeReceiptEvents", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "InstructionDrafts", "SELECT, UPDATE");
        Grant(migrationBuilder, WebRole, "StandaloneAuditEvidence", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "Triage", "SELECT, UPDATE");
        Grant(migrationBuilder, WebRole, "TriageHistory", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "TriageFindings", "SELECT, INSERT");
        Grant(migrationBuilder, WebRole, "TriageResponseEvidenceLinks", "SELECT, INSERT, DELETE");
        Grant(migrationBuilder, WebRole, "SentEmailEvidence", "SELECT");
        Grant(migrationBuilder, WebRole, "EmailResponseEvidence", "SELECT");

        // Durable intake, approved-mailbox, email-evidence, and external-work callers.
        Grant(migrationBuilder, WorkerRole, "ApprovedInboxPollStates", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WorkerRole, "ApprovedInboxPoisonMessages", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "IntakeMailRouteDecisions", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "IntakeStagedReceipts", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WorkerRole, "IntakeWorkItems", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WorkerRole, "IntakeEvaluations", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "IntakeReceipts", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "IntakeAssets", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "InstructionDrafts", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "IntakeReceiptEvents", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "Principals", "SELECT");
        Grant(migrationBuilder, WorkerRole, "ProviderDomainPackages", "SELECT");
        Grant(migrationBuilder, WorkerRole, "ProviderDomainEvidence", "SELECT");
        Grant(migrationBuilder, WorkerRole, "ProviderReferences", "SELECT");
        Grant(migrationBuilder, WorkerRole, "Triage", "SELECT, INSERT, UPDATE");
        Grant(migrationBuilder, WorkerRole, "TriageHistory", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "SentEmailEvidence", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "EmailResponseEvidence", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "ExternalWorkItems", "SELECT, UPDATE");
        Grant(migrationBuilder, WorkerRole, "Cases", "SELECT, UPDATE");
        Grant(migrationBuilder, WorkerRole, "CaseHistory", "SELECT, INSERT");
        Grant(migrationBuilder, WorkerRole, "CaseReportSentEvidence", "SELECT, INSERT");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
        {
            return;
        }

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime') IS NOT NULL
               AND IS_ROLEMEMBER(N'pegasus_web_runtime_role', N'pegasus_web_runtime') = 1
                ALTER ROLE [pegasus_web_runtime_role] DROP MEMBER [pegasus_web_runtime];
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime') IS NOT NULL
               AND IS_ROLEMEMBER(N'pegasus_worker_runtime_role', N'pegasus_worker_runtime') = 1
                ALTER ROLE [pegasus_worker_runtime_role] DROP MEMBER [pegasus_worker_runtime];

            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                DROP ROLE [pegasus_worker_runtime_role];
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                DROP ROLE [pegasus_web_runtime_role];
            """);
    }

    private static void Grant(
        MigrationBuilder migrationBuilder,
        string role,
        string table,
        string permissions) =>
        migrationBuilder.Sql($"GRANT {permissions} ON OBJECT::[dbo].[{table}] TO [{role}];");
}
