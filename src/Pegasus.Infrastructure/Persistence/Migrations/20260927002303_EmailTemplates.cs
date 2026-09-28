using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// E-mail templates in Administration (operator, 26 September 2026): one row
/// per purpose an Administrator has saved, holding its body, version and who
/// saved it. A purpose with no row uses its built-in body. Web alone reads and
/// writes the table and is denied DELETE; the Worker has no part in it and is
/// denied DELETE too. Additive.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260927002303_EmailTemplates")]
public partial class EmailTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EmailTemplates",
            columns: table => new
            {
                Purpose = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Body = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmailTemplates", x => x.Purpose);
            });

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[EmailTemplates] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[EmailTemplates] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                BEGIN
                    DENY DELETE ON OBJECT::[dbo].[EmailTemplates] TO [pegasus_worker_runtime_role];
                END;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "EmailTemplates");
    }
}
