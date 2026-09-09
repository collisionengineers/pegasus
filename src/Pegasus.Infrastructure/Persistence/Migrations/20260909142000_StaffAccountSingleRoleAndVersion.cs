using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Establishes the unreleased staff-account invariant: every retained Identity
/// user has one application role. Ambiguous or incomplete data is deliberately
/// reported for correction; this migration never selects a role on an
/// operator's behalf.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260909142000_StaffAccountSingleRoleAndVersion")]
public partial class StaffAccountSingleRoleAndVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Version",
            table: "AspNetUsers",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.Sql("""
            DECLARE @invalid nvarchar(max) = (
                SELECT STRING_AGG(
                    CONVERT(nvarchar(max), CONCAT(COALESCE(invalid.[UserName], N'<no username>'), N' [', CONVERT(nvarchar(36), invalid.Id), N']')),
                    N', ')
                FROM (SELECT u.Id, u.UserName FROM [AspNetUsers] AS u
                LEFT JOIN [AspNetUserRoles] AS ur ON ur.[UserId] = u.[Id]
                LEFT JOIN [AspNetRoles] AS r ON r.[Id] = ur.[RoleId]
                GROUP BY u.[Id], u.[UserName]
                HAVING COUNT(ur.[RoleId]) <> 1
                    OR MAX(r.[NormalizedName]) NOT IN (N'ADMINISTRATOR', N'ENGINEER', N'USER')) AS invalid);

            IF @invalid IS NOT NULL
            BEGIN
                RAISERROR(
                    N'Staff-account single-role migration stopped. Correct these users so each has exactly one supported role: %s',
                    16,
                    1,
                    @invalid);
            END;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_UserId",
            table: "AspNetUserRoles",
            column: "UserId",
            unique: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_AspNetUsers_Version",
            table: "AspNetUsers",
            sql: "[Version] >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_AspNetUsers_DefaultSignOffEligibility",
            table: "AspNetUsers",
            sql: "[IsDefaultSignOffEngineer] = 0 OR [IsSignOffEngineer] = 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_AspNetUsers_DefaultSignOffEligibility",
            table: "AspNetUsers");

        migrationBuilder.DropCheckConstraint(
            name: "CK_AspNetUsers_Version",
            table: "AspNetUsers");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUserRoles_UserId",
            table: "AspNetUserRoles");

        migrationBuilder.DropColumn(
            name: "Version",
            table: "AspNetUsers");
    }
}
