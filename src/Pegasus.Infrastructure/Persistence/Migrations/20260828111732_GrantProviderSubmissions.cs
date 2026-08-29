using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantProviderSubmissions : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // TICK-058: ProviderSubmissions is new in
        // 20260828111707_ProviderSubmissions and the least-privilege runtime
        // roles grant nothing on a table they have never heard of. Web hosts
        // the Provider API (API-01): it inserts one row per accepted
        // submission and reads rows back for idempotent replay and the
        // provider's own result lookup. The Worker processes the staged
        // files and reads the row to bind each one to the Principal whose
        // credential submitted it; it never writes one.
        //
        // The row is created when the submission is received and then
        // completed in place: the staged receipt id is only known once the
        // request has been durably retained, and the result lookup reads it
        // back to answer the provider. Web therefore holds UPDATE as well;
        // the Worker never writes. A submission is never removed, so no
        // DELETE is granted to either role.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireRuntimeRoles(migrationBuilder);
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ProviderSubmissions] TO [{WebRole}];");
            migrationBuilder.Sql(
                $"GRANT SELECT ON OBJECT::[dbo].[ProviderSubmissions] TO [{WorkerRole}];");
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
                $"REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ProviderSubmissions] FROM [{WebRole}];");
            migrationBuilder.Sql(
                $"REVOKE SELECT ON OBJECT::[dbo].[ProviderSubmissions] FROM [{WorkerRole}];");
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
