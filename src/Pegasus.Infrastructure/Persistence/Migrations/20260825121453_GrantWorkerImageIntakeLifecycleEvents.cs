using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantWorkerImageIntakeLifecycleEvents : Migration
    {
        private const string WorkerRole = "pegasus_worker_runtime_role";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                return;
            }

            migrationBuilder.Sql(
                $"""
                IF NOT EXISTS (
                    SELECT 1 FROM sys.database_principals
                    WHERE name = N'{WorkerRole}'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The Pegasus Worker runtime role is missing or invalid.', 1;
                """);
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [{WorkerRole}];");
            migrationBuilder.Sql(
                $"DENY UPDATE, DELETE ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [{WorkerRole}];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                return;
            }

            migrationBuilder.Sql(
                $"REVOKE SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] FROM [{WorkerRole}];");
        }
    }
}
