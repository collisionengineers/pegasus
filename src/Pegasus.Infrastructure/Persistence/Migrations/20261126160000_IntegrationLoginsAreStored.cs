using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The integrations' logins move into the database (R1288, ADR-0091): one
/// row per login, its values protected with the Web host's key ring. The
/// pending-link columns are for Brego's link, which a later change uses.
/// Additive: a new table, no row rewritten.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261126160000_IntegrationLoginsAreStored")]
public partial class IntegrationLoginsAreStored : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IntegrationLogins",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                ProtectedLogin = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ProtectedPendingLink = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PendingLinkExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Generation = table.Column<long>(type: "bigint", nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntegrationLogins", x => x.Id);
            });

        // The runtime roles' grants on the new table (RuntimePermissions.csv):
        // the Web reads, imports and replaces a login and renews Brego's
        // sign-in; nothing deletes one, and the Worker never touches one.
        migrationBuilder.Sql("""
            GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntegrationLogins] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[IntegrationLogins] TO [pegasus_web_runtime_role];
            DENY DELETE ON OBJECT::[dbo].[IntegrationLogins] TO [pegasus_worker_runtime_role];
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "IntegrationLogins");
    }
}
