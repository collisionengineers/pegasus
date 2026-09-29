using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Completing a Case's images re-evaluates its readiness and records the
/// outcome on the Case's <c>CaseDataSnapshots</c> row (FRD-13), where both
/// runtime roles held SELECT and INSERT only. Production refused every such
/// write: the Worker when photographs filed from a linked upload or a folded
/// Image-initiated case completed the images, and Web when a Case save flips
/// the readiness recorded there. Both roles gain UPDATE; DELETE stays denied.
///
/// A fold the refusal failed has already moved its files into the Case folder
/// and recorded nothing, so it is queued again: the Box fold replays as done
/// and the completion files the photographs.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260929150000_GrantCaseDataSnapshotUpdate")]
public partial class GrantCaseDataSnapshotUpdate : Migration
{
    private const string WebRole = "pegasus_web_runtime_role";
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
            $"GRANT UPDATE ON OBJECT::[dbo].[CaseDataSnapshots] TO [{WebRole}];");
        migrationBuilder.Sql(
            $"GRANT UPDATE ON OBJECT::[dbo].[CaseDataSnapshots] TO [{WorkerRole}];");
        migrationBuilder.Sql(
            """
            UPDATE [dbo].[ExternalWorkItems]
            SET [State] = N'pending',
                [DueAtUtc] = SYSUTCDATETIME(),
                [LeaseToken] = NULL,
                [LeaseExpiresAtUtc] = NULL,
                [CompletedAtUtc] = NULL,
                [FailureCode] = NULL,
                [FailureReason] = NULL
            WHERE [Kind] = N'merge_image_case_custody'
              AND [State] = N'failed'
              AND [FailureCode] = N'custody_unexpected_failure:DbUpdateException';
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
            $"REVOKE UPDATE ON OBJECT::[dbo].[CaseDataSnapshots] FROM [{WorkerRole}];");
        migrationBuilder.Sql(
            $"REVOKE UPDATE ON OBJECT::[dbo].[CaseDataSnapshots] FROM [{WebRole}];");
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
