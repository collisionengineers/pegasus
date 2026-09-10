using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A valuation preset is removed, never deleted: the maintained list and any
/// new selection lose it, while the row stays so the recorded valuations and
/// the history that name it remain readable. The unique label index is
/// deliberately left unfiltered, which keeps a removed preset's label
/// reserved rather than reusable by a second record.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260910105000_SoftRemoveValuationPresets")]
public partial class SoftRemoveValuationPresets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "RemovedAtUtc",
            table: "ValuationPresets",
            type: "datetimeoffset",
            nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Valuation preset removals are retained in this pre-release migration.");
}
