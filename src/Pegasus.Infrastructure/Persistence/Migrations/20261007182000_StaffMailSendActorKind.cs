using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Automation Actor sends reports and e-mail as staff do (ADR-0064,
/// operator 7 October 2026), so a send records who sent it: a member of staff
/// or the Automation Actor. Every existing send was a staff send and takes
/// <c>Staff</c>. Additive; the runtime roles' table-level grants on
/// <c>StaffMailSendOperations</c> cover the column. An older release never
/// names the column, so its inserts take the default and read as staff sends.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261007182000_StaffMailSendActorKind")]
public partial class StaffMailSendActorKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActorKind",
            table: "StaffMailSendOperations",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Staff");
        migrationBuilder.AddCheckConstraint(
            name: "CK_StaffMailSendOperations_ActorKind",
            table: "StaffMailSendOperations",
            sql: "[ActorKind] IN ('Staff', 'Automation')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_StaffMailSendOperations_ActorKind",
            table: "StaffMailSendOperations");
        migrationBuilder.DropColumn(name: "ActorKind", table: "StaffMailSendOperations");
    }
}
