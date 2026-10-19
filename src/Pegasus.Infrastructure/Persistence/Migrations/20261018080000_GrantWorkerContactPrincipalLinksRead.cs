using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Worker's mail route evaluation reads the intermediaries from the Contacts
/// directory: an intermediary is a Claim Source linked to a Principal (operator,
/// 15 October 2026). It already reads <c>Organizations</c> and <c>Principals</c>;
/// it gains SELECT on <c>ContactPrincipalLinks</c> and never writes a link.
/// DELETE stays denied. Web's link grants are unchanged.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261018080000_GrantWorkerContactPrincipalLinksRead")]
public partial class GrantWorkerContactPrincipalLinksRead : Migration
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
            $"GRANT SELECT ON OBJECT::[dbo].[ContactPrincipalLinks] TO [{WorkerRole}];");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (!IsSqlServer())
        {
            return;
        }

        migrationBuilder.Sql(
            $"REVOKE SELECT ON OBJECT::[dbo].[ContactPrincipalLinks] FROM [{WorkerRole}];");
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
