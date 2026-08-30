using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantProviderSubmissionAcceptRecovery : Migration
    {
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // AUTO-012: the Provider API accept path writes the submission row in
        // Web, retains its source through the shared durable-intake path, and
        // then records the staged-receipt back-reference. The existing Worker
        // reconciliation timer repairs that back-reference after a process
        // loss, so the Worker needs UPDATE on this table in addition to the
        // SELECT grant established by 20260828111732_GrantProviderSubmissions.
        // It never creates or deletes provider submissions; the Web grant
        // remains unchanged and DELETE stays denied for both roles.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireRuntimeRoles(migrationBuilder);
            migrationBuilder.Sql(
                $"GRANT UPDATE ON OBJECT::[dbo].[ProviderSubmissions] TO [{WorkerRole}];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            migrationBuilder.Sql(
                $"REVOKE UPDATE ON OBJECT::[dbo].[ProviderSubmissions] FROM [{WorkerRole}];");
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
