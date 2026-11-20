using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Automation runs Glass's sessions of its own (operator, 18 November
/// 2026; R1177): each session records who runs it, Staff for every session so
/// far, and an Automation session records what it has done as JSON. An
/// Automation session names no member of staff, so its UserId is empty and
/// the session table's key to the staff accounts is dropped; the UserId index
/// stays. Non-additive (one dropped foreign key) and so on the destructive
/// route (R605, R760): no row is rewritten, and every existing row reads as
/// Staff. Forward only: the key can be put back only while no Automation
/// session row exists.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261118140000_GlassSessionOwner")]
public partial class GlassSessionOwner : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_GlassRepairEstimateSessions_AspNetUsers_UserId",
            table: "GlassRepairEstimateSessions");

        migrationBuilder.AddColumn<string>(
            name: "OwnerKind",
            table: "GlassRepairEstimateSessions",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Staff");

        migrationBuilder.AddColumn<string>(
            name: "AutomationJson",
            table: "GlassRepairEstimateSessions",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "An Automation Glass's session names no member of staff, so the session table's key to the staff accounts is not put back.");
}
