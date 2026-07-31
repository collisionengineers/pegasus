using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729171000_CaseAcceptanceReplay")]
public partial class CaseAcceptanceReplay : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ExpectedIntakeVersion",
            table: "CaseIntakeLinks",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AcceptanceCommandMaterialJson",
            table: "CaseIntakeLinks",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AcceptanceCommandFingerprint",
            table: "CaseIntakeLinks",
            fixedLength: true,
            maxLength: 64,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AcceptanceCommandFingerprint",
            table: "CaseIntakeLinks");

        migrationBuilder.DropColumn(
            name: "AcceptanceCommandMaterialJson",
            table: "CaseIntakeLinks");

        migrationBuilder.DropColumn(
            name: "ExpectedIntakeVersion",
            table: "CaseIntakeLinks");
    }
}
