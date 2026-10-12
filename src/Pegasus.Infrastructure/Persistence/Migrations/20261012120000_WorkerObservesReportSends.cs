using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Worker's Sent poll observes a report send and links its evidence, and
/// two of the writes it makes there were never granted (QDOS26099, 12 October
/// 2026). Observing a report send freezes the Sent repair specification, an
/// INSERT on <c>CaseRepairSpecificationSnapshots</c>, where the Worker held
/// SELECT only, so every report send stayed Submitted. Linking the Inspection
/// report's evidence once the Audit exists sets the primary work's evidence,
/// an UPDATE on <c>CaseWorks</c>, where the Worker held SELECT and INSERT.
/// DELETE stays denied on both.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261012120000_WorkerObservesReportSends")]
public partial class WorkerObservesReportSends : Migration
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
            $"""
            GRANT INSERT ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [{WorkerRole}];
            GRANT UPDATE ON OBJECT::[dbo].[CaseWorks] TO [{WorkerRole}];
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        migrationBuilder.Sql(
            $"""
            REVOKE UPDATE ON OBJECT::[dbo].[CaseWorks] FROM [{WorkerRole}];
            REVOKE INSERT ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] FROM [{WorkerRole}];
            """);
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
