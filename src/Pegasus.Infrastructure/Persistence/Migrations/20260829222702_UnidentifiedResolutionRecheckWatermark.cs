using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnidentifiedResolutionRecheckWatermark : Migration
    {
        private const string WorkerRole = "pegasus_worker_runtime_role";

        // INTK-048: the manual-association version an automation resolution's
        // destination was last reconciled against. The reconciliation sweep
        // selected rows by comparing association timestamps against
        // ResolvedAtUtc, but a recheck that finds the destination unchanged
        // writes no resolution, so that comparison never advanced: the row was
        // re-selected on every pass and, holding the oldest ResolvedAtUtc,
        // occupied the head of the bounded oldest-first page until later stale
        // resolutions were never rechecked at all. The association version is
        // monotonic per receipt, moves on every link, unlink and relink, and
        // needs no clock.
        //
        // NULL means "resolved, never yet rechecked", which is what every
        // existing row is: each gets exactly one recheck pass, which records
        // the version it examined. No backfill.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReconciledAssociationVersion",
                table: "UnidentifiedItems",
                type: "bigint",
                nullable: true);

            // The Worker runs this sweep and now writes this column, so the
            // permission the new path needs is proved here rather than
            // discovered on the deployed estate — local runs are
            // full-privilege and would never notice. No GRANT is issued: the
            // Worker role's object-level UPDATE on dbo.UnidentifiedItems
            // (20260819115323_UnidentifiedWork) already covers every column of
            // the table, including one added afterwards. This asserts exactly
            // that, and fails the migration loudly if the estate has drifted
            // to a column-scoped grant that would not.
            if (IsSqlServer())
            {
                migrationBuilder.Sql(
                    $"""
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.database_permissions AS granted
                        JOIN sys.database_principals AS grantee
                            ON grantee.principal_id = granted.grantee_principal_id
                        WHERE grantee.name = N'{WorkerRole}'
                          AND granted.class = 1
                          AND granted.major_id = OBJECT_ID(N'dbo.UnidentifiedItems')
                          AND granted.minor_id = 0
                          AND granted.permission_name = N'UPDATE'
                          AND granted.state IN ('G', 'W'))
                        THROW 51000, 'The Pegasus Worker runtime role lacks object-level UPDATE on dbo.UnidentifiedItems.', 1;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn(
                name: "ReconciledAssociationVersion",
                table: "UnidentifiedItems");

        private bool IsSqlServer() =>
            string.Equals(
                ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);
    }
}
