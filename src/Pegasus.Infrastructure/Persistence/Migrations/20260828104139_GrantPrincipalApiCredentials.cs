using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantPrincipalApiCredentials : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";

        // TICK-061: PrincipalApiCredentials is new in
        // 20260828104130_PrincipalApiCredentials and the least-privilege
        // runtime roles grant nothing on a table they have never heard of.
        // Only the Web runtime touches it — Administrators issue, reset,
        // pause, resume and revoke from the application, and the Provider API
        // (TICK-058) verifies a presented secret inside the same Web process.
        // The Worker never authenticates a provider, so it gets no grant.
        //
        // SELECT, INSERT and UPDATE: one row per Principal is created once and
        // then rotated or moved through its states in place. DELETE is
        // deliberately absent: a revoked credential stays as the record of
        // what was revoked.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireRuntimeRole(migrationBuilder);
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PrincipalApiCredentials] TO [{WebRole}];");
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
                $"REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[PrincipalApiCredentials] FROM [{WebRole}];");
        }

        private bool IsSqlServer() =>
            string.Equals(
                ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

        private static void RequireRuntimeRole(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.database_principals
                    WHERE name = N'pegasus_web_runtime_role'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
                """);
    }
}
