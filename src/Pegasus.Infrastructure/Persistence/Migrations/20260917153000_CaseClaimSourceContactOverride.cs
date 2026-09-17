using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260917153000_CaseClaimSourceContactOverride")]
public partial class CaseClaimSourceContactOverride : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClaimSourceOverrideContactEmailAddress",
            table: "CaseDataSnapshots",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClaimSourceOverrideContactName",
            table: "CaseDataSnapshots",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClaimSourceOverrideContactTelephone",
            table: "CaseDataSnapshots",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClaimSourceOverrideContactEmailAddress",
            table: "CaseDataSnapshots");

        migrationBuilder.DropColumn(
            name: "ClaimSourceOverrideContactName",
            table: "CaseDataSnapshots");

        migrationBuilder.DropColumn(
            name: "ClaimSourceOverrideContactTelephone",
            table: "CaseDataSnapshots");
    }
}
