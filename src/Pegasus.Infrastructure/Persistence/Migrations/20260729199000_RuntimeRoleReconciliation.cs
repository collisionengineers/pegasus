using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729199000_RuntimeRoleReconciliation")]
public sealed class RuntimeRoleReconciliation : Migration
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    private static readonly string[] RuntimeTables =
    [
        "ActionHistory",
        "ApplicationInitializations",
        "ApprovedInboxPoisonMessages",
        "ApprovedInboxPollStates",
        "ApprovedMailboxes",
        "ApprovedSentPollOutcomes",
        "ApprovedSentPollStates",
        "AspNetRoleClaims",
        "AspNetRoles",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserRoles",
        "AspNetUserTokens",
        "AspNetUsers",
        "BoxFileRequests",
        "CaseDataFields",
        "CaseDataSnapshots",
        "CaseDocuments",
        "CaseDueChasers",
        "CaseDueWork",
        "CaseEditLeaseOperations",
        "CaseEngineerFindings",
        "CaseHistory",
        "CaseIntakeLinks",
        "CaseManualChases",
        "CaseReportApprovals",
        "CaseReportSentEvidence",
        "CaseSequences",
        "CaseTasks",
        "CaseWorkflowEvents",
        "CaseWorkflows",
        "Cases",
        "DocumentOccurrences",
        "DocumentVersions",
        "EmailResponseEvidence",
        "EvaFirstHandoffProxies",
        "EvaHandoffOperations",
        "EvaHandoffRevisions",
        "ExternalWorkItems",
        "InstructionDrafts",
        "IntakeAssets",
        "IntakeEvaluations",
        "IntakeMailRouteDecisions",
        "IntakeManualAssociations",
        "IntakeMutationHistory",
        "IntakeReceiptEvents",
        "IntakeReceipts",
        "IntakeStagedReceipts",
        "IntakeWorkItems",
        "OpenIddictApplications",
        "OpenIddictAuthorizations",
        "OpenIddictScopes",
        "OpenIddictTokens",
        "OrganizationAdministrationOperations",
        "OrganizationRoles",
        "Organizations",
        "PrincipalSequenceLineages",
        "Principals",
        "ProviderDomainEvidence",
        "ProviderDomainPackages",
        "ProviderReferences",
        "RequestUploadLinks",
        "RequestUploadReceipts",
        "SecurityEvents",
        "SentEmailEvidence",
        "StandaloneAuditEvidence",
        "Triage",
        "TriageFindings",
        "TriageHistory",
        "TriageResponseEvidenceLinks",
        "VehicleConfirmations",
        "VehicleLookupObservations",
        "VehicleLookupRequests",
        "WorkflowConfigurations"
    ];

    private static readonly (string Table, string Permissions)[] WebGrants =
    [
        ("ActionHistory", "SELECT, INSERT"),
        ("ApprovedInboxPoisonMessages", "SELECT"),
        ("ApprovedInboxPollStates", "SELECT, UPDATE"),
        ("ApprovedMailboxes", "SELECT, INSERT, UPDATE"),
        ("ApprovedSentPollOutcomes", "SELECT"),
        ("ApprovedSentPollStates", "SELECT, UPDATE"),
        ("AspNetRoleClaims", "SELECT"),
        ("AspNetRoles", "SELECT"),
        ("AspNetUserClaims", "SELECT"),
        ("AspNetUserRoles", "SELECT, INSERT, DELETE"),
        ("AspNetUsers", "SELECT, INSERT, UPDATE"),
        ("BoxFileRequests", "SELECT, INSERT, UPDATE"),
        ("CaseDataFields", "SELECT, INSERT, UPDATE, DELETE"),
        ("CaseDataSnapshots", "SELECT, INSERT"),
        ("CaseDocuments", "SELECT, INSERT"),
        ("CaseDueChasers", "SELECT"),
        ("CaseDueWork", "SELECT, INSERT, UPDATE"),
        ("CaseEditLeaseOperations", "SELECT, INSERT"),
        ("CaseEngineerFindings", "SELECT, INSERT"),
        ("CaseHistory", "SELECT, INSERT"),
        ("CaseIntakeLinks", "SELECT, INSERT"),
        ("CaseManualChases", "SELECT, INSERT"),
        ("CaseReportApprovals", "SELECT, INSERT"),
        ("CaseReportSentEvidence", "SELECT, UPDATE"),
        ("CaseSequences", "SELECT, INSERT, UPDATE"),
        ("CaseTasks", "SELECT, INSERT, UPDATE"),
        ("CaseWorkflowEvents", "SELECT, INSERT"),
        ("CaseWorkflows", "SELECT, INSERT, UPDATE"),
        ("Cases", "SELECT, INSERT, UPDATE"),
        ("DocumentOccurrences", "SELECT, INSERT"),
        ("DocumentVersions", "SELECT, INSERT, UPDATE"),
        ("EmailResponseEvidence", "SELECT"),
        ("EvaFirstHandoffProxies", "SELECT, INSERT"),
        ("EvaHandoffOperations", "SELECT, INSERT"),
        ("EvaHandoffRevisions", "SELECT, INSERT"),
        ("ExternalWorkItems", "SELECT, INSERT, UPDATE"),
        ("InstructionDrafts", "SELECT, INSERT, UPDATE"),
        ("IntakeAssets", "SELECT"),
        ("IntakeEvaluations", "SELECT"),
        ("IntakeMailRouteDecisions", "SELECT"),
        ("IntakeManualAssociations", "SELECT, INSERT, UPDATE"),
        ("IntakeMutationHistory", "SELECT, INSERT"),
        ("IntakeReceiptEvents", "INSERT"),
        ("IntakeReceipts", "SELECT, UPDATE"),
        ("IntakeStagedReceipts", "SELECT, INSERT"),
        ("IntakeWorkItems", "SELECT, INSERT, UPDATE"),
        ("OpenIddictApplications", "SELECT, INSERT, UPDATE"),
        ("OpenIddictAuthorizations", "SELECT, INSERT, UPDATE"),
        ("OpenIddictScopes", "SELECT"),
        ("OpenIddictTokens", "SELECT, INSERT, UPDATE"),
        ("OrganizationAdministrationOperations", "SELECT, INSERT"),
        ("OrganizationRoles", "SELECT, INSERT, DELETE"),
        ("Organizations", "SELECT, INSERT, UPDATE"),
        ("PrincipalSequenceLineages", "SELECT, INSERT"),
        ("Principals", "SELECT, INSERT, UPDATE"),
        ("RequestUploadLinks", "SELECT, INSERT, UPDATE"),
        ("RequestUploadReceipts", "SELECT, INSERT"),
        ("SecurityEvents", "SELECT, INSERT"),
        ("SentEmailEvidence", "SELECT"),
        ("StandaloneAuditEvidence", "SELECT, INSERT"),
        ("Triage", "SELECT, UPDATE"),
        ("TriageFindings", "SELECT, INSERT"),
        ("TriageHistory", "SELECT, INSERT"),
        ("TriageResponseEvidenceLinks", "SELECT, INSERT, DELETE"),
        ("VehicleConfirmations", "SELECT, INSERT"),
        ("VehicleLookupObservations", "SELECT"),
        ("VehicleLookupRequests", "SELECT, INSERT"),
        ("WorkflowConfigurations", "SELECT, UPDATE")
    ];

    private static readonly (string Table, string Permissions)[] WorkerGrants =
    [
        ("ActionHistory", "SELECT, INSERT"),
        ("ApprovedInboxPoisonMessages", "SELECT, INSERT"),
        ("ApprovedInboxPollStates", "SELECT, INSERT, UPDATE"),
        ("ApprovedMailboxes", "SELECT"),
        ("ApprovedSentPollOutcomes", "SELECT, INSERT"),
        ("ApprovedSentPollStates", "SELECT, INSERT, UPDATE"),
        ("CaseDueChasers", "SELECT, INSERT, UPDATE"),
        ("CaseDueWork", "SELECT, UPDATE"),
        ("CaseEditLeaseOperations", "SELECT"),
        ("CaseHistory", "INSERT"),
        ("CaseIntakeLinks", "SELECT"),
        ("CaseReportApprovals", "SELECT"),
        ("CaseReportSentEvidence", "SELECT, INSERT, UPDATE"),
        ("CaseWorkflowEvents", "SELECT, INSERT"),
        ("CaseWorkflows", "SELECT, UPDATE"),
        ("Cases", "SELECT, UPDATE"),
        ("EmailResponseEvidence", "SELECT, INSERT"),
        ("ExternalWorkItems", "SELECT, UPDATE"),
        ("InstructionDrafts", "SELECT, INSERT, UPDATE"),
        ("IntakeAssets", "SELECT, INSERT"),
        ("IntakeEvaluations", "SELECT, INSERT"),
        ("IntakeMailRouteDecisions", "SELECT, INSERT, UPDATE"),
        ("IntakeManualAssociations", "SELECT"),
        ("IntakeReceiptEvents", "INSERT"),
        ("IntakeReceipts", "SELECT, INSERT, UPDATE"),
        ("IntakeStagedReceipts", "SELECT, INSERT, UPDATE"),
        ("IntakeWorkItems", "SELECT, INSERT, UPDATE"),
        ("ProviderDomainEvidence", "SELECT"),
        ("ProviderDomainPackages", "SELECT"),
        ("ProviderReferences", "SELECT"),
        ("RequestUploadLinks", "SELECT"),
        ("SentEmailEvidence", "SELECT, INSERT, UPDATE"),
        ("Triage", "SELECT, INSERT, UPDATE"),
        ("TriageHistory", "SELECT, INSERT"),
        ("TriageResponseEvidenceLinks", "SELECT, INSERT"),
        ("VehicleLookupObservations", "INSERT"),
        ("VehicleLookupRequests", "SELECT")
    ];

    private static readonly (string Table, string Permissions)[] PreviousWebGrants =
    [
        ("ActionHistory", "SELECT, INSERT"),
        ("ApprovedSentPollOutcomes", "SELECT"),
        ("ApprovedSentPollStates", "SELECT, UPDATE"),
        ("AspNetRoleClaims", "SELECT"),
        ("AspNetRoles", "SELECT"),
        ("AspNetUserClaims", "SELECT, INSERT, UPDATE, DELETE"),
        ("AspNetUserLogins", "SELECT, INSERT, DELETE"),
        ("AspNetUserRoles", "SELECT, INSERT, DELETE"),
        ("AspNetUserTokens", "SELECT, INSERT, UPDATE, DELETE"),
        ("AspNetUsers", "SELECT, INSERT, UPDATE, DELETE"),
        ("CaseDocuments", "SELECT, INSERT"),
        ("CaseDueWork", "SELECT, INSERT, UPDATE"),
        ("CaseEngineerFindings", "SELECT, INSERT"),
        ("CaseHistory", "SELECT, INSERT"),
        ("CaseIntakeLinks", "SELECT, INSERT"),
        ("CaseManualChases", "SELECT, INSERT"),
        ("CaseReportApprovals", "SELECT, INSERT"),
        ("CaseReportSentEvidence", "SELECT, UPDATE"),
        ("CaseSequences", "SELECT, INSERT, UPDATE"),
        ("CaseWorkflowEvents", "SELECT, INSERT"),
        ("CaseWorkflows", "SELECT, INSERT, UPDATE"),
        ("Cases", "SELECT, INSERT, UPDATE"),
        ("DocumentOccurrences", "SELECT, INSERT"),
        ("DocumentVersions", "SELECT, INSERT, UPDATE"),
        ("EmailResponseEvidence", "SELECT"),
        ("EvaFirstHandoffProxies", "SELECT, INSERT"),
        ("EvaHandoffOperations", "SELECT, INSERT"),
        ("EvaHandoffRevisions", "SELECT, INSERT"),
        ("ExternalWorkItems", "INSERT"),
        ("InstructionDrafts", "SELECT, UPDATE"),
        ("IntakeAssets", "SELECT"),
        ("IntakeMailRouteDecisions", "SELECT"),
        ("IntakeReceiptEvents", "SELECT, INSERT"),
        ("IntakeReceipts", "SELECT, UPDATE"),
        ("IntakeStagedReceipts", "SELECT, INSERT"),
        ("IntakeWorkItems", "SELECT, INSERT"),
        ("OpenIddictApplications", "SELECT"),
        ("OpenIddictAuthorizations", "SELECT, INSERT, UPDATE, DELETE"),
        ("OpenIddictScopes", "SELECT"),
        ("OpenIddictTokens", "SELECT, INSERT, UPDATE, DELETE"),
        ("OrganizationRoles", "SELECT"),
        ("Organizations", "SELECT"),
        ("PrincipalSequenceLineages", "SELECT"),
        ("Principals", "SELECT"),
        ("RequestUploadLinks", "SELECT, INSERT, UPDATE"),
        ("RequestUploadReceipts", "SELECT, INSERT"),
        ("SecurityEvents", "SELECT, INSERT"),
        ("SentEmailEvidence", "SELECT"),
        ("StandaloneAuditEvidence", "SELECT, INSERT"),
        ("Triage", "SELECT, UPDATE"),
        ("TriageFindings", "SELECT, INSERT"),
        ("TriageHistory", "SELECT, INSERT"),
        ("TriageResponseEvidenceLinks", "SELECT, INSERT, DELETE")
    ];

    private static readonly (string Table, string Permissions)[] PreviousWorkerGrants =
    [
        ("ActionHistory", "SELECT, INSERT"),
        ("ApprovedInboxPoisonMessages", "SELECT, INSERT"),
        ("ApprovedInboxPollStates", "SELECT, INSERT, UPDATE"),
        ("ApprovedMailboxes", "SELECT"),
        ("ApprovedSentPollOutcomes", "SELECT, INSERT"),
        ("ApprovedSentPollStates", "SELECT, INSERT, UPDATE"),
        ("CaseDueWork", "SELECT"),
        ("CaseHistory", "SELECT, INSERT"),
        ("CaseReportApprovals", "SELECT"),
        ("CaseReportSentEvidence", "SELECT, INSERT, UPDATE"),
        ("CaseWorkflowEvents", "SELECT, INSERT"),
        ("CaseWorkflows", "SELECT, UPDATE"),
        ("Cases", "SELECT, UPDATE"),
        ("EmailResponseEvidence", "SELECT, INSERT"),
        ("ExternalWorkItems", "SELECT, UPDATE"),
        ("InstructionDrafts", "SELECT, INSERT"),
        ("IntakeAssets", "SELECT, INSERT"),
        ("IntakeEvaluations", "SELECT, INSERT"),
        ("IntakeMailRouteDecisions", "SELECT, INSERT"),
        ("IntakeReceiptEvents", "SELECT, INSERT"),
        ("IntakeReceipts", "SELECT, INSERT"),
        ("IntakeStagedReceipts", "SELECT, INSERT, UPDATE"),
        ("IntakeWorkItems", "SELECT, INSERT, UPDATE"),
        ("Principals", "SELECT"),
        ("ProviderDomainEvidence", "SELECT"),
        ("ProviderDomainPackages", "SELECT"),
        ("ProviderReferences", "SELECT"),
        ("SentEmailEvidence", "SELECT, INSERT, UPDATE"),
        ("Triage", "SELECT, INSERT, UPDATE"),
        ("TriageHistory", "SELECT, INSERT"),
        ("TriageResponseEvidenceLinks", "SELECT, INSERT")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }
        RequireManagedRoles(migrationBuilder);
        ResetObjectDml(migrationBuilder, WebRole);
        ResetObjectDml(migrationBuilder, WorkerRole);
        Grant(migrationBuilder, WebRole, WebGrants);
        Grant(migrationBuilder, WorkerRole, WorkerGrants);

        foreach (var table in RuntimeTables)
        {
            if (!WebDeleteIsRequired(table))
            {
                DenyDelete(migrationBuilder, WebRole, table);
            }
            DenyDelete(migrationBuilder, WorkerRole, table);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }
        RequireManagedRoles(migrationBuilder);
        ResetObjectDml(migrationBuilder, WebRole);
        ResetObjectDml(migrationBuilder, WorkerRole);
        Grant(migrationBuilder, WebRole, PreviousWebGrants);
        Grant(migrationBuilder, WorkerRole, PreviousWorkerGrants);
    }

    private bool IsSqlServer() =>
        string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal);

    private static void RequireManagedRoles(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            $"""
            IF NOT EXISTS (
                SELECT 1
                FROM sys.database_principals
                WHERE name = N'{WebRole}'
                  AND [type] = 'R'
                  AND is_fixed_role = 0
                  AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
            IF NOT EXISTS (
                SELECT 1
                FROM sys.database_principals
                WHERE name = N'{WorkerRole}'
                  AND [type] = 'R'
                  AND is_fixed_role = 0
                  AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
            """);

    private static void ResetObjectDml(MigrationBuilder migrationBuilder, string role)
    {
        foreach (var table in RuntimeTables)
        {
            migrationBuilder.Sql(
                $"REVOKE SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[{table}] FROM [{role}];");
        }
    }

    private static void Grant(
        MigrationBuilder migrationBuilder,
        string role,
        IReadOnlyList<(string Table, string Permissions)> grants)
    {
        foreach (var (table, permissions) in grants)
        {
            migrationBuilder.Sql(
                $"GRANT {permissions} ON OBJECT::[dbo].[{table}] TO [{role}];");
        }
    }

    private static void DenyDelete(
        MigrationBuilder migrationBuilder,
        string role,
        string table) =>
        migrationBuilder.Sql(
            $"DENY DELETE ON OBJECT::[dbo].[{table}] TO [{role}];");

    private static bool WebDeleteIsRequired(string table) => table is
        "AspNetUserRoles" or
        "CaseDataFields" or
        "OrganizationRoles" or
        "TriageResponseEvidenceLinks";
}
