using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A removed valuation preset frees its label (R432; R530, 29 October 2026), the
/// rule FRD-17 already states for Case list presets: the unique index on
/// <c>ValuationPresets.Label</c> counts only the presets kept. Additive in
/// effect: every label the unfiltered index allowed the filtered one allows,
/// and no row changes. Forward-only, because once a removed preset's label is
/// reused the unfiltered index cannot be rebuilt.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261029120000_RemovedValuationPresetFreesItsLabel")]
public partial class RemovedValuationPresetFreesItsLabel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ValuationPresets_Label",
            table: "ValuationPresets");

        migrationBuilder.CreateIndex(
            name: "IX_ValuationPresets_Label",
            table: "ValuationPresets",
            column: "Label",
            unique: true,
            filter: "[RemovedAtUtc] IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "A removed valuation preset's label may already be reused; the unfiltered index cannot be restored.");
}
