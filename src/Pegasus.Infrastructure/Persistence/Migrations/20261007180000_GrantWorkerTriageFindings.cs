using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Triage linked automatically fills the instruction Case's empty findings
/// from its current finding (operator, 7 October 2026). The automatic link
/// runs as the Worker, which held no permission on <c>TriageFindings</c>, so
/// the role gains SELECT. The assessment cells, workflow, history and report
/// generations it writes with it are already granted. DELETE stays denied.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007180000_GrantWorkerTriageFindings")]
public partial class GrantWorkerTriageFindings : Migration
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
            $"GRANT SELECT ON OBJECT::[dbo].[TriageFindings] TO [{WorkerRole}];");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        migrationBuilder.Sql(
            $"REVOKE SELECT ON OBJECT::[dbo].[TriageFindings] FROM [{WorkerRole}];");
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
