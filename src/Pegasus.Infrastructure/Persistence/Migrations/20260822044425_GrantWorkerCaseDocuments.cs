using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantWorkerCaseDocuments : Migration
    {
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // DOCS-008: DOCS-007 moved case-document registration into the Worker's
        // custody processor, but the Worker runtime role's grant matrix
        // (20260729199000_RuntimeRoleReconciliation) lists these three tables
        // for the Web role only — when that baseline was written, only Web
        // created case documents. On the deployed estate the Worker therefore
        // held the DELETE deny and no grant at all, so every case created after
        // release 17 uploaded its evidence to Box and was then refused the
        // record write. That rolled the whole custody transaction back and
        // reported "Case evidence could not be stored" over files sitting in
        // Box. Local and LocalDB tests run full-privilege and never exercise
        // the least-privilege role, which is why CI stayed green — the same
        // blind spot that produced 20260821095500.
        //
        // The permission strings are the Web role's own entries for these
        // tables, so both callers stay described by one vocabulary. UPDATE is
        // needed only on DocumentVersions, where EfDocumentCustodyStore clears
        // IsCurrent on superseded versions. Additive to the reconciliation
        // baseline; DELETE remains denied.
        private static readonly (string Table, string Permissions)[] Grants =
        [
            ("CaseDocuments", "SELECT, INSERT"),
            ("DocumentOccurrences", "SELECT, INSERT"),
            ("DocumentVersions", "SELECT, INSERT, UPDATE")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireWorkerRole(migrationBuilder);
            foreach (var (table, permissions) in Grants)
            {
                migrationBuilder.Sql(
                    $"GRANT {permissions} ON OBJECT::[dbo].[{table}] TO [{WorkerRole}];");
            }
        }

        /// <inheritdoc />
        // Down removes only the permissions this migration adds, leaving the
        // reconciliation baseline — which grants the Worker nothing on these
        // tables beyond its DELETE deny — exactly as it was.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            foreach (var (table, permissions) in Grants)
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
