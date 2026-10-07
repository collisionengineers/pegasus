using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Principal API submission's id is now derived from its Principal and
/// idempotency key, and its intake source identity is what makes a retry a
/// replay (operator, 7 October 2026). The key, the body digest, the claim
/// number echo, the presenting key id and the staged-receipt back-reference
/// were each a second record of something intake or the declaration already
/// holds, so they go, and nothing updates a submission row any more.
/// Existing rows carry random ids and the retired declaration shape, so they
/// are removed rather than converted: this is forward-only, and the test
/// estate's intake is wiped before it runs.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007180000_SimplifyPrincipalSubmissions")]
public partial class SimplifyPrincipalSubmissions : Migration
{
    private const string WebRole = "pegasus_web_runtime_role";
    private const string WorkerRole = "pegasus_worker_runtime_role";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql("DELETE FROM [dbo].[PrincipalSubmissions];");

        migrationBuilder.DropIndex(
            name: "IX_PrincipalSubmissions_PrincipalId_IdempotencyKey",
            table: "PrincipalSubmissions");

        foreach (var column in new[] { "KeyId", "IdempotencyKey", "BodySha256", "PrincipalReference", "StagedReceiptId" })
        {
            migrationBuilder.DropColumn(name: column, table: "PrincipalSubmissions");
        }

        migrationBuilder.CreateIndex(
            name: "IX_PrincipalSubmissions_PrincipalId",
            table: "PrincipalSubmissions",
            column: "PrincipalId");

        migrationBuilder.Sql($"REVOKE UPDATE ON OBJECT::[dbo].[PrincipalSubmissions] FROM [{WebRole}];");
        migrationBuilder.Sql($"REVOKE UPDATE ON OBJECT::[dbo].[PrincipalSubmissions] FROM [{WorkerRole}];");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "SimplifyPrincipalSubmissions is forward-only: removed submissions and their columns are not restored.");
}
