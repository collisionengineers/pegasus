using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729189000_IdentityBootstrapAndOAuthAdministration")]
public sealed class IdentityBootstrapAndOAuthAdministration : Migration
{
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        RequireSqlServer();

        migrationBuilder.AddColumn<string>(
            name: "TargetIdentity",
            table: "ApplicationInitializations",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql(
            """
            IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'ADMINISTRATOR')
            BEGIN
                INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
                VALUES ('B6DC43DE-34A8-438D-BCE9-84DA67554DC0', 'bootstrap-administrator', N'Administrator', N'ADMINISTRATOR');
            END;

            IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'ENGINEER')
            BEGIN
                INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
                VALUES ('63D5F2EB-C8E8-4D09-A625-3D9D04BF77FC', 'bootstrap-engineer', N'Engineer', N'ENGINEER');
            END;

            IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'USER')
            BEGIN
                INSERT INTO [AspNetRoles] ([Id], [ConcurrencyStamp], [Name], [NormalizedName])
                VALUES ('9B71B14E-E6F0-456C-8393-3543FD684EA6', 'bootstrap-user', N'User', N'USER');
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RequireSqlServer();

        migrationBuilder.DropColumn(
            name: "TargetIdentity",
            table: "ApplicationInitializations");
    }

    private void RequireSqlServer()
    {
        if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"Migration provider '{ActiveProvider}' is not supported.");
        }
    }
}
