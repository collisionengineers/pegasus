using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantImageIntakeLifecycleUpdates : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";
        private const string WorkerRole = "pegasus_worker_runtime_role";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                return;
            }

            RequireRuntimeRole(migrationBuilder, WebRole);
            RequireRuntimeRole(migrationBuilder, WorkerRole);
            migrationBuilder.Sql($"GRANT UPDATE ON OBJECT::[dbo].[ImageIntakes] TO [{WebRole}];");
            migrationBuilder.Sql($"GRANT UPDATE ON OBJECT::[dbo].[ImageIntakes] TO [{WorkerRole}];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                return;
            }

            migrationBuilder.Sql($"REVOKE UPDATE ON OBJECT::[dbo].[ImageIntakes] FROM [{WorkerRole}];");
            migrationBuilder.Sql($"REVOKE UPDATE ON OBJECT::[dbo].[ImageIntakes] FROM [{WebRole}];");
        }

        private static void RequireRuntimeRole(MigrationBuilder migrationBuilder, string role) =>
            migrationBuilder.Sql(
                $"""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.database_principals
                    WHERE name = N'{role}'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The Pegasus runtime role {role} is missing or invalid.', 1;
                """);
    }
}
