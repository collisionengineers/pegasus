using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerCaseCreationGrants : Migration
    {
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // The Worker runtime role predates automatic allocation moving to the
        // Worker (INT-25). Its grant matrix in
        // 20260729199000_RuntimeRoleReconciliation therefore never included the
        // object permissions the automatic case-acceptance transaction
        // (EfCaseAcceptanceStore.AcceptOnceAsync, which INSERTs the whole case
        // aggregate in one batch) and the automatic standalone-Audit evidence
        // recording require. Local/LocalDB tests run full-privilege and never
        // exercised the least-privilege role, so this only ever failed against the
        // deployed estate. These grants are additive to the reconciliation
        // baseline; DELETE remains denied everywhere for the Worker.
        private static readonly (string Table, string Permissions)[] WorkerGrants =
        [
            ("StandaloneAuditEvidence", "SELECT, INSERT"),
            ("Cases", "SELECT, INSERT, UPDATE"),
            ("CaseSequences", "SELECT, INSERT, UPDATE"),
            ("CaseMatchIndex", "SELECT, INSERT, UPDATE"),
            ("CaseIntakeLinks", "SELECT, INSERT"),
            ("CaseHistory", "SELECT, INSERT"),
            ("CaseWorkflows", "SELECT, INSERT, UPDATE"),
            ("CaseDataSnapshots", "SELECT, INSERT"),
            ("CaseDataFields", "SELECT, INSERT, UPDATE"),
            ("CaseDueWork", "SELECT, INSERT, UPDATE"),
            ("ExternalWorkItems", "SELECT, INSERT, UPDATE"),
            ("IntakeMutationHistory", "SELECT, INSERT"),
            ("Principals", "SELECT, UPDATE"),
            ("PrincipalSequenceLineages", "SELECT, INSERT"),
            ("Organizations", "SELECT"),
            ("OrganizationRoles", "SELECT"),
            ("VehicleConfirmations", "SELECT, INSERT"),
            ("WorkflowConfigurations", "SELECT")
        ];

        // Down removes only the permissions this migration adds, preserving the
        // reconciliation baseline the Worker already held on partially-granted
        // tables (e.g. Cases keeps its baseline SELECT, UPDATE; only INSERT is
        // revoked).
        private static readonly (string Table, string Permissions)[] WorkerRevokes =
        [
            ("StandaloneAuditEvidence", "SELECT, INSERT"),
            ("Cases", "INSERT"),
            ("CaseSequences", "SELECT, INSERT, UPDATE"),
            ("CaseMatchIndex", "INSERT, UPDATE"),
            ("CaseIntakeLinks", "INSERT"),
            ("CaseHistory", "SELECT"),
            ("CaseWorkflows", "INSERT"),
            ("CaseDataSnapshots", "SELECT, INSERT"),
            ("CaseDataFields", "SELECT, INSERT, UPDATE"),
            ("CaseDueWork", "INSERT"),
            ("ExternalWorkItems", "INSERT"),
            ("IntakeMutationHistory", "SELECT, INSERT"),
            ("Principals", "SELECT, UPDATE"),
            ("PrincipalSequenceLineages", "SELECT, INSERT"),
            ("Organizations", "SELECT"),
            ("OrganizationRoles", "SELECT"),
            ("VehicleConfirmations", "SELECT, INSERT"),
            ("WorkflowConfigurations", "SELECT")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireWorkerRole(migrationBuilder);
            foreach (var (table, permissions) in WorkerGrants)
            {
                migrationBuilder.Sql(
                    $"GRANT {permissions} ON OBJECT::[dbo].[{table}] TO [{WorkerRole}];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            foreach (var (table, permissions) in WorkerRevokes)
            {
                migrationBuilder.Sql(
                    $"REVOKE {permissions} ON OBJECT::[dbo].[{table}] FROM [{WorkerRole}];");
            }
        }

        private bool IsSqlServer() =>
            string.Equals(
                ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

        private static void RequireWorkerRole(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.database_principals
                    WHERE name = N'pegasus_worker_runtime_role'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
                """);
    }
}
