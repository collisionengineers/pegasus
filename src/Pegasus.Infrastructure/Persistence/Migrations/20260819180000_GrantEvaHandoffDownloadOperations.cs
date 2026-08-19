using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GrantEvaHandoffDownloadOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EvaHandoffDownloadOperations (20260811122654_CaseCustodyEvaRecovery) was
            // created with no grant anywhere: production shows zero permission rows for
            // any principal on it. EvaHandoffStore.cs replays a SELECT (line ~194) and
            // Adds a row (line ~272) from the Web runtime only; there is no UPDATE and
            // no Remove, and the Worker never touches this table. Mirrors the sibling
            // EvaHandoffOperations/EvaHandoffRevisions tables' production shape exactly:
            // Web holds SELECT, INSERT with DELETE denied; the Worker holds no grant but
            // the same defensive DENY DELETE those siblings received from the blanket
            // 20260729199000_RuntimeRoleReconciliation reconciliation loop — this table
            // simply didn't exist yet on that date to be swept up in it.
            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[EvaHandoffDownloadOperations] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "DENY DELETE ON OBJECT::[dbo].[EvaHandoffDownloadOperations] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "DENY DELETE ON OBJECT::[dbo].[EvaHandoffDownloadOperations] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "REVOKE DELETE ON OBJECT::[dbo].[EvaHandoffDownloadOperations] FROM [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "REVOKE DELETE ON OBJECT::[dbo].[EvaHandoffDownloadOperations] FROM [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "REVOKE SELECT, INSERT ON OBJECT::[dbo].[EvaHandoffDownloadOperations] FROM [pegasus_web_runtime_role];");
            }
        }
    }
}
