using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Dismiss and Restore on the Inbox write the message row's dismissed-at and
/// dismissed-by cells (FRD-20). The Web role held SELECT only on
/// <c>RetainedMailboxMessages</c>, so every Dismiss and Restore was refused by the
/// database. Web gains UPDATE on the table; the bootstrap audit rejects column
/// grants. INSERT stays with the Worker and DELETE stays denied.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260929091000_GrantWebRetainedMailDismissal")]
public partial class GrantWebRetainedMailDismissal : Migration
{
    private const string WebRole = "pegasus_web_runtime_role";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        RequireRuntimeRoles(migrationBuilder);
        migrationBuilder.Sql(
            $"GRANT UPDATE ON OBJECT::[dbo].[RetainedMailboxMessages] TO [{WebRole}];");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        migrationBuilder.Sql(
            $"REVOKE UPDATE ON OBJECT::[dbo].[RetainedMailboxMessages] FROM [{WebRole}];");
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
