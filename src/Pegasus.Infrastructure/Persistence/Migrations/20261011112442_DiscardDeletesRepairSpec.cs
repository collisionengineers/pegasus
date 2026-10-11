using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Discard deletes a repair spec (operator, 11 October 2026): the spec, its
    /// lines and its numbered versions go, and the Case history keeps who
    /// discarded it, when and why. Every spec already discarded is deleted the
    /// same way, and the Discarded state, its three columns and their check
    /// constraint go with it. Destructive and forward-only.
    /// </summary>
    public partial class DiscardDeletesRepairSpec : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
                throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

            migrationBuilder.Sql(
                """
                DELETE estimateLine
                FROM dbo.CaseEstimateLines AS estimateLine
                JOIN dbo.CaseRepairSpecifications AS specification
                    ON specification.Id = estimateLine.RepairSpecificationId
                WHERE specification.[State] = N'Discarded';

                DELETE versionRow
                FROM dbo.CaseRepairSpecificationSnapshots AS versionRow
                JOIN dbo.CaseRepairSpecifications AS specification
                    ON specification.Id = versionRow.SpecificationId
                    AND specification.WorkId = versionRow.WorkId
                WHERE specification.[State] = N'Discarded';

                DELETE FROM dbo.CaseRepairSpecifications
                WHERE [State] = N'Discarded';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_Discard",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseRepairSpecifications_State",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "DiscardReason",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "DiscardedAtUtc",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "DiscardedBy",
                table: "CaseRepairSpecifications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseRepairSpecifications_State",
                table: "CaseRepairSpecifications",
                sql: "[State] IN ('Draft')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "DiscardDeletesRepairSpec is forward-only: it deletes every discarded repair spec with its lines and versions. Restore from an approved backup instead.");
    }
}
