using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A Principal has no salvage matrix (operator, 25 November 2026; R1263): the
/// Contacts tab that kept it and the Case's Salvage value pre-fill it fed are
/// removed, and its column goes. No production Principal held one (read 25
/// November 2026). Non-additive and forward only: a stored matrix is not kept.
/// The Case's own salvage category, value and share are untouched.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261125120000_DropPrincipalSalvageMatrix")]
public partial class DropPrincipalSalvageMatrix : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(
            name: "SalvageMatrixJson",
            table: "Principals");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "The salvage matrix is retired; a Principal's stored matrix is not kept.");
}
