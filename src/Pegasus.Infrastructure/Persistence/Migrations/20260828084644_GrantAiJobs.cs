using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantAiJobs : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";

        // AUTO-011: AiJobs is new in 20260828084601_AiJobs and the
        // least-privilege runtime roles grant nothing on a table they have
        // never heard of. Only the Web runtime touches the ledger — staff
        // create, cancel and confirm from the application and external AI
        // clients work it through the /mcp ingress hosted by Web. The Worker
        // runs no AI timer (ADR-0035, EPIC-011 D5), so it gets no grant.
        //
        // SELECT, INSERT and UPDATE: rows are created once and then move
        // through their states in place, exactly as AiWorkRequests does
        // (20260803205759). DELETE is deliberately absent: a job is a
        // permanent record.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireRuntimeRole(migrationBuilder);
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[AiJobs] TO [{WebRole}];");
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
                $"REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[AiJobs] FROM [{WebRole}];");
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
