using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>Allow the Web runtime role to remove only rows deleted by the staff account command.</summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260923120000_StaffAccountDeletionRuntimePermissions")]
public partial class StaffAccountDeletionRuntimePermissions : Migration
{
    private static readonly string[] DeleteTables =
    [
        "AspNetUsers",
        "UserExternalCredentials",
        "GlassRepairEstimateSessions",
        "StaffNotifications"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;
        foreach (var table in DeleteTables)
        {
            migrationBuilder.Sql($"REVOKE DELETE ON OBJECT::[dbo].[{table}] FROM [pegasus_web_runtime_role];");
            migrationBuilder.Sql($"GRANT DELETE ON OBJECT::[dbo].[{table}] TO [pegasus_web_runtime_role];");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;
        foreach (var table in DeleteTables)
        {
            migrationBuilder.Sql($"REVOKE DELETE ON OBJECT::[dbo].[{table}] FROM [pegasus_web_runtime_role];");
            migrationBuilder.Sql($"DENY DELETE ON OBJECT::[dbo].[{table}] TO [pegasus_web_runtime_role];");
        }
    }
}
