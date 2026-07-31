using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729187000_OrganizationPrincipalAdministration")]
public partial class OrganizationPrincipalAdministration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NormalizedName",
            table: "Organizations",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: false,
            computedColumnSql: "UPPER(LTRIM(RTRIM([Name])))",
            stored: true);

        migrationBuilder.CreateTable(
            name: "OrganizationAdministrationOperations",
            columns: table => new
            {
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CommandKind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_OrganizationAdministrationOperations",
                    value => value.OperationKey);
            });

        migrationBuilder.Sql(
            """
            CREATE UNIQUE INDEX [IX_Organizations_NormalizedName]
                ON [Organizations] ([NormalizedName]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationAdministrationOperations");

        migrationBuilder.DropIndex(
            name: "IX_Organizations_NormalizedName",
            table: "Organizations");

        migrationBuilder.DropColumn(
            name: "NormalizedName",
            table: "Organizations");
    }
}
