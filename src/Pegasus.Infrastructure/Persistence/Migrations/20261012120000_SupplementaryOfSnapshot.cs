using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A repair specification's supplementary statement names the frozen
    /// version it changes against, not another specification (v38 P3,
    /// operator, 12 October 2026): an estimate imported onto the spec
    /// supplements the version the last sent report used, else the grid the
    /// import replaced. The column is renamed and every value it held, each a
    /// specification id, is cleared with its reason, flag and statement. The
    /// snapshot kinds admit BeforeImport, the grid an import replaced.
    /// Data-only beyond the rename and the constraint; forward-only.
    /// </summary>
    public partial class SupplementaryOfSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
                throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecificationSnapshots_Kind",
                table: "CaseRepairSpecificationSnapshots");
            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecificationSnapshots_Kind",
                table: "CaseRepairSpecificationSnapshots",
                sql: "[Kind] IN ('Imported', 'BeforeScaling', 'Scaled', 'ScalingRemoved', 'BeforeRestore', 'Restored', 'Sent', 'BeforeImport')");

            migrationBuilder.RenameColumn(
                name: "SupplementaryOfSpecificationId",
                table: "CaseRepairSpecifications",
                newName: "SupplementaryOfSnapshotId");

            migrationBuilder.Sql(
                """
                UPDATE dbo.CaseRepairSpecifications
                SET SupplementaryOfSnapshotId = NULL,
                    SupplementaryReason = NULL,
                    SupplementaryExplainOnReport = 0,
                    SupplementaryStatement = NULL
                WHERE SupplementaryOfSnapshotId IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("This migration is forward-only.");
        }
    }
}
