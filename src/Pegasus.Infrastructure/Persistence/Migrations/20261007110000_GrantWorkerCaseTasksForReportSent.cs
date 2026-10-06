using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// When the Worker's Sent-evidence poll links a Pegasus report send to its Case, Report sent
/// creates the after-send tasks the send recorded (FRD-13, CASE-20) in the same transaction.
/// The Worker role held no grant on <c>CaseTasks</c>, so it gains SELECT (the replay check)
/// and INSERT; it never updates a task. DELETE stays denied. The list is read from the send's
/// own row in <c>StaffMailSendOperations</c>, and the sender's role from <c>ActionHistory</c>,
/// both of which the Worker already reads. It already holds SELECT and INSERT on
/// <c>CaseWorkflowEvents</c>, INSERT on <c>ActionHistory</c> and UPDATE on
/// <c>CaseWorkflows</c>, which the same transaction writes. Web already holds what staff
/// Mark report sent needs.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007110000_GrantWorkerCaseTasksForReportSent")]
public partial class GrantWorkerCaseTasksForReportSent : Migration
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
            $"GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseTasks] TO [{WorkerRole}];");
        migrationBuilder.Sql(
            $"DENY DELETE ON OBJECT::[dbo].[CaseTasks] TO [{WorkerRole}];");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        migrationBuilder.Sql(
            $"REVOKE SELECT, INSERT ON OBJECT::[dbo].[CaseTasks] FROM [{WorkerRole}];");
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
                WHERE name = N'pegasus_worker_runtime_role'
                  AND [type] = 'R'
                  AND is_fixed_role = 0
                  AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
            """);
}
