using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Release 54 made the Worker's vehicle-lookup fill read the confirmed
/// <c>vehicle.mileage_source</c> assessment field (report freshness) and write
/// the derived Vehicle type through the assessment field writer. Both run in
/// the Worker's lookup transaction, so the Worker role needs the same
/// read/insert/update rights on <c>CaseAssessmentFields</c> that Web already
/// holds. DELETE stays denied for both roles.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260917140000_GrantWorkerCaseAssessmentFields")]
public partial class GrantWorkerCaseAssessmentFields : Migration
{
    private const string WorkerRole = "pegasus_worker_runtime_role";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        RequireRuntimeRoles(migrationBuilder);
        migrationBuilder.Sql(
            $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseAssessmentFields] TO [{WorkerRole}];");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        migrationBuilder.Sql(
            $"REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseAssessmentFields] FROM [{WorkerRole}];");
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
