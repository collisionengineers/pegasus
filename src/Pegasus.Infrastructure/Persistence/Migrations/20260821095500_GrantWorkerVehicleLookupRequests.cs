using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantWorkerVehicleLookupRequests : Migration
    {
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // CASE-008 moved automatic vehicle-lookup enqueueing into the Worker's
        // reconciliation sweep, but the Worker runtime role's grant matrix
        // (20260729199000_RuntimeRoleReconciliation) holds only SELECT on
        // VehicleLookupRequests — the request row INSERT the sweep performs was
        // denied on the deployed estate and surfaced as zero lookups enqueued.
        // Local/LocalDB tests run full-privilege and never exercise the
        // least-privilege role, so this only ever failed against the deployed
        // estate. Additive to the reconciliation baseline; DELETE remains
        // denied everywhere for the Worker.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            RequireWorkerRole(migrationBuilder);
            migrationBuilder.Sql(
                $"GRANT INSERT ON OBJECT::[dbo].[VehicleLookupRequests] TO [{WorkerRole}];");
        }

        // Down removes only the permission this migration adds, preserving the
        // reconciliation baseline SELECT the Worker already held.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!IsSqlServer())
            {
                return;
            }

            migrationBuilder.Sql(
                $"REVOKE INSERT ON OBJECT::[dbo].[VehicleLookupRequests] FROM [{WorkerRole}];");
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
