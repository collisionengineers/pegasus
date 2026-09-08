using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260907210000_ReportInputInvalidationPermissions")]
public sealed class ReportInputInvalidationPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer")
        {
            return;
        }

        migrationBuilder.Sql("""
            IF DATABASE_PRINCIPAL_ID('pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT UPDATE ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_web_runtime_role];
                GRANT UPDATE ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID('pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT,UPDATE ON OBJECT::[dbo].[CaseReportGenerations] TO [pegasus_worker_runtime_role];
                GRANT SELECT ON OBJECT::[dbo].[GeneratedCaseArtifacts] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer")
        {
            return;
        }

        migrationBuilder.Sql("""
            IF DATABASE_PRINCIPAL_ID('pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                REVOKE UPDATE ON OBJECT::[dbo].[CaseReportGenerations] FROM [pegasus_web_runtime_role];
                REVOKE UPDATE ON OBJECT::[dbo].[GeneratedCaseArtifacts] FROM [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID('pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                REVOKE SELECT,UPDATE ON OBJECT::[dbo].[CaseReportGenerations] FROM [pegasus_worker_runtime_role];
                REVOKE SELECT ON OBJECT::[dbo].[GeneratedCaseArtifacts] FROM [pegasus_worker_runtime_role];
            END;
            """);
    }
}
