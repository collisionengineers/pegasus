using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantEvaSubmissions : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // EXT-04: EvaSubmissions is new in 20260827143132_EvaApiSubmissions and
        // the least-privilege runtime roles grant nothing on a table they have
        // never heard of. Both runtimes write it — Web when an operator presses
        // Send to EVA, the Worker when a principal's automatic setting submits
        // a case that reached Review — so both need the same two permissions.
        //
        // This rides the same diff as the table on purpose. CI runs against
        // full-privilege LocalDB and never exercises these roles, so a missing
        // grant passes every test and then refuses every submission in
        // production, exactly as DOCS-008 and 20260821095500 did before it.
        //
        // SELECT as well as INSERT because the once-per-case rule and the
        // replay check both read prior attempts before deciding whether to
        // submit. UPDATE is deliberately absent: an attempt is a fact about a
        // moment and is never edited, only followed by another row.
        //
        // Principals already carries SELECT for both roles from the
        // reconciliation baseline, so the two new toggle columns need no grant
        // of their own — SQL Server table permissions cover added columns.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireRuntimeRoles(migrationBuilder);
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT ON OBJECT::[dbo].[EvaSubmissions] TO [{WebRole}];");
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT ON OBJECT::[dbo].[EvaSubmissions] TO [{WorkerRole}];");
        }

        /// <inheritdoc />
        // Down revokes only what this migration granted. The table itself is
        // dropped by the migration that created it, not here.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            migrationBuilder.Sql(
                $"REVOKE SELECT, INSERT ON OBJECT::[dbo].[EvaSubmissions] FROM [{WebRole}];");
            migrationBuilder.Sql(
                $"REVOKE SELECT, INSERT ON OBJECT::[dbo].[EvaSubmissions] FROM [{WorkerRole}];");
        }

        private bool IsSqlServer() =>
            string.Equals(
                ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

        private static void RequireRuntimeRoles(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.database_principals
                    WHERE name = N'pegasus_web_runtime_role'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;

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
