using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The garage authorisation milestone of a Garage authorisation and invoice
/// Audit (operator, 17 November 2026; R1142): one nullable column holding when
/// the Engineer marked the garage authorised, null on every Case there is.
/// Additive: no row is rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261117220000_GarageAuthorisation")]
public partial class GarageAuthorisation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "GarageAuthorisedAtUtc",
            table: "CaseWorkflows",
            type: "datetimeoffset",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "GarageAuthorisedAtUtc", table: "CaseWorkflows");
    }
}
