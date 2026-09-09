using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260909148000_CanonicalContactLocations")]
public sealed class CanonicalContactLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Postcode", "Organizations", type: "nvarchar(20)", maxLength: 20, nullable: true);
        migrationBuilder.DropTable("OrganizationDirectoryEntries");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("The superseded pre-release location directory has been removed.");
}
